using B2CTestDriver.methods;
using B2CTestDriver.models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using NUnit.Framework.Internal;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Support.Extensions;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Threading.Tasks;
using System.Net.Http;
using Tools;

namespace B2CTestDriver
{
    [TestFixture]
    public class B2CTestDriver
    {
        IWebDriver driver;
        private static AppSettings _configuration;
        private static Dictionary<string, string> _keys;
        private static int _testNumber = 0;
        private static TelemetryLog telemetryLog;
        
        private static HttpClient httpClient = new HttpClient();

        const string telemetryMetricPass = "Pass";
        const string telemetryMetricFail = "Fail";

        internal static AppSettings LoadJSON(string path)
        {
            var jsonText = ReadFile(path);

            if (String.IsNullOrEmpty(jsonText))
            {
                throw new Exception("appsettings.json was not present or is empty.");
            }

            try
            {
                return JsonConvert.DeserializeObject<AppSettings>(jsonText);
            }
            catch 
            {
                throw new Exception("appsettings.json is not valid json");
            }

            return new AppSettings();
        }

        [OneTimeSetUp]
        public void SetUp()
        {
            var keysPath = TestContext.CurrentContext.WorkDirectory;
           
            var jsonText = ReadFile(keysPath + @"\keys.json");
            if (!String.IsNullOrEmpty(jsonText))
            {
                _keys = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonText);
            }

            //Below code is to get the drivers folder path dynamically.

            //You can also specify chromedriver.exe path dircly ex: C:/MyProject/Project/drivers

            var browserEnv = _configuration.TestConfiguration.Environment;
            telemetryLog.TrackEvent("information", "browser", browserEnv);

            // If we ever want to pass driver location as a parameter
            var driverPath = TestContext.CurrentContext.TestDirectory;

            if (browserEnv == "Chrome")
            {
                //Creates the ChomeDriver object, Executes tests on Google Chrome

                driver = new ChromeDriver(driverPath + @"\drivers\");
            }
            else if (browserEnv == "Firefox")
            {
                // Specify Correct location of geckodriver.exe folder path. Ex: C:/Project/drivers

                driver = new FirefoxDriver(driverPath + @"\drivers\");
            }
            else
            {
                telemetryLog.TrackEvent("exception", "browser environment", "Unrecognized Browser Environment!");
                throw new Exception("Unrecognized Browser Environment!");
            }
        
        }

        private static IEnumerable<List<Page[]>> TestStarter()
        {
            var testPath = TestContext.CurrentContext.WorkDirectory;

            // TestCaseSource runs before OneTimeSetup
            testPath = EnvVar("appsettings.json", testPath + @"\appsettings.json");
            if (_configuration == null)
                _configuration = LoadJSON(testPath);

            // Init AppInsights.  Or not
            string instrumentationKey = EnvVar("appInsightsInstrumentationKey");
            telemetryLog = new TelemetryLog(instrumentationKey);
            telemetryLog.TrackEvent("------------------\nB2CTestDriver Started", "time", DateTime.Now.ToString());
            
            var testSuite = new List<List<Page[]>>();

            foreach (var test in _configuration.Tests)
            {
                string localPath = test.Contains("http")
                    ? test
                    : Path.Combine(TestContext.CurrentContext.WorkDirectory, "Tests", $"{test}.json");
                var jsonText = ReadFile(localPath);

                if (String.IsNullOrEmpty(jsonText))
                {
                    string errorText = $"file {test} is missing or is empty";
                    telemetryLog.LogException(errorText);
                    throw new Exception(errorText);
                }
                else
                {
                    try
                    {
                        var testPages = JsonConvert.DeserializeObject<OrderedDictionary>(jsonText);
                        List<Page[]> tempList = new List<Page[]>();

                        foreach (DictionaryEntry pageActionList in testPages)
                        {
                            var testValues = (pageActionList.Value as JArray).ToObject<Page[]>();
                            tempList.Add(testValues);
                        }

                        testSuite.Add(tempList);
                    }
                    catch (Exception ex)
                    {
                        telemetryLog.TrackEvent("error", "invalid json", jsonText);
                        Assert.Fail($"invalid json: {jsonText}");
                    }
                }
            }

            foreach (List<Page[]> testFlow in testSuite)
            {
                yield return testFlow;
            }
        }


        [Test, TestCaseSource(nameof(TestStarter))]
        public async Task ExecuteFlow(List<Page[]> test)
        {
            var currentTestName = _configuration.Tests[_testNumber++];
            TestContextWrite($"Execution: {currentTestName}");
            for (int i = 0; i < test.Count; i++)
            {
                var pageActions = test[i];
                int j = 0;
                if (i == 0)
                {
                    // Start of new test, we need to navigate to the test start
                    if (pageActions[0].InputType == "testCaseStart")
                    {
                        // Increment j as we are handling the first element
                        j++;
                        try
                        {
                            driver.Navigate().GoToUrl(pageActions[0].Value);

                            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(_configuration.TestConfiguration.TimeOut));
                            // If no ID we just want to check for URL
                            if (String.IsNullOrEmpty(pageActions[0].Id))
                                wait.Until(webDriver => webDriver.Url.Contains(pageActions[0].Value));
                            else
                            {
                                wait.Until(webDriver => webDriver.Url.Contains(pageActions[0].Value) && webDriver.FindElements(By.Id(pageActions[0].Id)).Count > 0);
                            }
                        }
                        catch (WebDriverTimeoutException)
                        {
                            if (!driver.Url.Contains(pageActions[0].Value))
                            {
                                AssertFail($"Test {currentTestName}: Expected URL {pageActions[0].Value}, but current URL is {driver.Url}");
                            }
                            else if (String.IsNullOrEmpty(pageActions[0].Id))
                            {
                                AssertFail($"Test {currentTestName}: URL {pageActions[0].Id} did not load within the {_configuration.TestConfiguration.TimeOut} second time period.");
                            }
                            else
                            {
                                AssertFail($"Test {currentTestName}: URL {pageActions[0].Value} did not load a visible element {pageActions[0].Id} within the {_configuration.TestConfiguration.TimeOut} second time period.");
                            }
                        }
                        catch (Exception ex)
                        {
                            ExceptionMessage(ex);
                        }
                    }
                    else
                    {
                        AssertFail("Test {currentTestName}: Invalid test. There was no navigation to a page to start.");
                    }
                }
                else
                {
                    // Otherwise new page and we need to check if first element we want to interact with is loaded
                    var existenceToCheck = 0;

                    while (String.IsNullOrEmpty(pageActions[existenceToCheck].Id))
                    {
                        // Looking for the first thing we want to interact with in test
                        existenceToCheck++;
                    }

                    try
                    {
                        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(_configuration.TestConfiguration.TimeOut));
                        wait.Until(webDriver => webDriver.FindElements(By.Id(pageActions[existenceToCheck].Id)).Count > 0);
                    }
                    catch (WebDriverTimeoutException)
                    {
                        AssertFail($"Test {currentTestName}: URL {pageActions[0].Value} did not load a visible element {pageActions[0].Id} within the {_configuration.TestConfiguration.TimeOut} second time period.");
                    }
                    catch (Exception ex)
                    {
                        ExceptionMessage(ex);
                    }
                }

                for (; j < pageActions.Length; j++)
                {
                    if (!pageActions[j].InputType.StartsWith("Fn::") && pageActions[j].InputType != "testCaseComplete")
                    {
                        // Check that the element we are looking for is visible
                        try
                        {
                            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(_configuration.TestConfiguration.TimeOut));
                            wait.Until(driver => driver.FindElement(By.Id(pageActions[j].Id)).Displayed);
                        }
                        catch (WebDriverTimeoutException)
                        {
                            AssertFail($"Test {currentTestName}: Next element {pageActions[j].Id} was not completed within the timeout period of {_configuration.TestConfiguration.TimeOut} second(s).");
                        }
                        catch (Exception ex)
                        {
                            ExceptionMessage(ex);
                        }
                    }
                    if (pageActions[j].InputType == "Text")
                    {
                        driver.FindElement(By.Id(pageActions[j].Id)).SendKeys(pageActions[j].Value);
                    }
                    else if (pageActions[j].InputType == "Button")
                    {
                        try
                        {
                            var buttonId = pageActions[j].Id;
                            // triggers for both buttons and links
                            driver.ExecuteJavaScript($"$('#{buttonId}')[0].click()");
                        }
                        catch (JavaScriptException)
                        {
                            AssertFail($"Test {currentTestName}: Button with ID: {pageActions[j].Id} was not visible on the page.");
                        }
                        catch (Exception ex)
                        {
                            ExceptionMessage(ex);
                        }
                    }
                    else if (pageActions[j].InputType == "link")
                    {
                        try
                        {
                            driver.ExecuteJavaScript($"document.getElementById('{pageActions[j].Id}').click()");
                        }
                        catch (JavaScriptException)
                        {
                            AssertFail($"Test {currentTestName}: Button with ID: {pageActions[j].Id} was not visible on the page.");
                        }
                    }
                    else if (pageActions[j].InputType == "Dropdown")
                    {
                        try
                        {
                            driver.ExecuteJavaScript($"$('#{pageActions[j].Id}').val('{pageActions[j].Value}')");
                        }
                        catch (JavaScriptException)
                        {
                            AssertFail($"Test {currentTestName}: Dropdown with ID: {pageActions[j].Id} was not visible on the page.");
                        }
                    }
                    else if (pageActions[j].InputType == "Checkbox")
                    {
                        try
                        {
                          driver.ExecuteJavaScript($"$('#{pageActions[j].Id}').trigger('click')");
                        }
                        catch (JavaScriptException)
                        {
                            AssertFail($"Test {currentTestName}: Checkbox with ID: {pageActions[j].Id} was not visible on the page.");
                        }
                    }
                    else if (pageActions[j].InputType.Contains("Fn::"))
                    {
                        var fnName = pageActions[j].InputType.Substring(4);
                        switch (fnName)
                        {
                            case "otpEmail":
                                try
                                {
                                    var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(_configuration.TestConfiguration.TimeOut));
                                    wait.Until(driver => driver.FindElement(By.Id("emailVerificationControl_but_verify_code")).Displayed);
                                }
                                catch (WebDriverTimeoutException)
                                {
                                    AssertFail($"Test {currentTestName}: Next element {pageActions[0].Id} was not completed within the timeout period of {_configuration.TestConfiguration.TimeOut} second(s).");
                                }
                                var otpCode = await B2CMethods.GetEmailOTP(
                                    driver.FindElement(By.Id(pageActions[j].Id)).GetAttribute("value"),
                                    _keys["otpFunctionAppKey"], _keys["otpFunctionApp"],
                                    _configuration.TestConfiguration.OTP_Age);
                                driver.FindElement(By.Id("verificationCode")).SendKeys(otpCode);
                                driver.ExecuteJavaScript("$('#verificationCode').trigger('focus')");
                                break;
                            case "newRandomUser":
                                var newRandomUser = B2CMethods.NewRandomUser(pageActions[j].Value);
                                TestContextWrite($"Test {currentTestName}: New user ID: {newRandomUser}");
                                driver.FindElement(By.Id(pageActions[j].Id)).SendKeys(newRandomUser);
                                break;
                        }
                    }
                    else if (pageActions[j].InputType == "testCaseComplete")
                    {
                        try
                        {
                            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(_configuration.TestConfiguration.TimeOut));
                            // If no ID we just want to check for URL (preference is to also look for an id)
                            if (String.IsNullOrEmpty(pageActions[j].Id))
                                wait.Until(webDriver => webDriver.Url.Contains(pageActions[j].Value));
                            else
                            {
                                wait.Until(webDriver => webDriver.Url.Contains(pageActions[j].Value) && webDriver.FindElements(By.Id(pageActions[j].Id)).Count > 0);
                            }
                        }
                        catch (WebDriverTimeoutException)
                        {
                            AssertFail($"Test {currentTestName}: URL {pageActions[j].Value} did not load within the {_configuration.TestConfiguration.TimeOut} second time period.");
                        }
                        catch (Exception ex)
                        {
                            ExceptionMessage(ex);
                        }

                        if (String.IsNullOrEmpty(pageActions[j].Id))
                        {
                            AssertPass($"Test {currentTestName}: Successfully landed on page: {pageActions[j].Value}");
                        }
                        else
                        {
                            AssertPass($"Test {currentTestName}: Successfully landed on page: {pageActions[j].Value} with element possessing ID: {pageActions[j].Id}");
                        }
                    }
                }
            }
            AssertFail("Test completion not configured.");
        }


        /// <summary>
        /// log failure messages /status
        /// </summary>
        /// <param name="message"></param>
        public void AssertFail(string message)
        {
            telemetryLog.TrackMetric(telemetryMetricFail, 1);
            telemetryLog.TrackEvent("assert fail", "assertion", message);
            Assert.Fail(message);
        }


        /// <summary>
        /// log success messsage / status
        /// </summary>
        /// <param name="message"></param>
        public void AssertPass(string message)
        {
            telemetryLog.TrackMetric(telemetryMetricPass, 1);
            telemetryLog.TrackEvent("assert pass", "assertion", message);
            Assert.Pass(message);
        }

        
        /// <summary>
        /// log exceptions
        /// </summary>
        /// <param name="ex"></param>
        public void ExceptionMessage(Exception ex)
        {
            telemetryLog.TrackMetric(telemetryMetricFail, 1);
            telemetryLog.TrackEvent("exception", "exception message", ex.ToString());
        }


        /// <summary>
        /// log information
        /// </summary>
        /// <param name="message"></param>
        public void TestContextWrite(string message)
        {
            telemetryLog.TrackEvent("information", "message", message);
            TestContext.Write(message);
        }


        /// <summary>
        /// read data from path, either local file or web file
        /// </summary>
        /// <param name="path">local file path or URL</param>
        /// <returns>contents of file as a string if success, zero length string otherwise</returns>
        static string ReadFile(string path)
        {
            string text = "";

            try
            {
                if (path.Substring(0, 4).ToLower() == "http")
                {
                    text = httpClient.GetStringAsync(path).Result;
                }
                else
                {
                    text = File.ReadAllText(path);
                }
            }
            catch { }
            return text;
        }


        /// <summary>
        /// return the value of the environment variable 
        /// or the default value if the environment variable does not exist
        /// </summary>
        /// <param name="key">environment variable name</param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        static string EnvVar(string key, string defaultValue = "")
        {
            string value = Environment.GetEnvironmentVariable(key);
            return string.IsNullOrEmpty(value) ? defaultValue : value;
        }


        [OneTimeTearDown]
        public void TearDown()
        {
            telemetryLog.TrackEvent("B2CTestDriver Completed", "information", $"{DateTime.Now}");
            telemetryLog.Flush();

            driver.Quit();
        }
    }
}