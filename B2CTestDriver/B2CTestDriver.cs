using B2CTestDriver.methods;
using B2CTestDriver.models;
using Newtonsoft.Json;
using NUnit.Framework;
using NUnit.Framework.Internal;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Support.Extensions;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
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
        
        private static bool useBlobStorage = false;
        private static AzureBlobStorageFrame blobFrame = null;

        const string telemetryMetricPass = "Pass";
        const string telemetryMetricFail = "Fail";

        internal static AppSettings LoadJSON(string path)
        {
            var jsonText = ReadFile(path + @"\appsettings.json", false);

            if (String.IsNullOrEmpty(jsonText))
            {
                throw new Exception("appsettings.json was not present or is empty.");
            }

            try
            {
                return JsonConvert.DeserializeObject<AppSettings>(jsonText);
            }
            catch { }

            return new AppSettings();
        }

        [OneTimeSetUp]
        public void SetUp()
        {
            var keysPath = TestContext.CurrentContext.WorkDirectory;
           
            var jsonText = ReadFile(keysPath + @"\keys.json", useBlobStorage);
            if (!String.IsNullOrEmpty(jsonText))
            {
                _keys = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonText);
            }

            //Below code is to get the drivers folder path dynamically.

            //You can also specify chromedriver.exe path dircly ex: C:/MyProject/Project/drivers

            var browserEnv = _configuration.TestConfiguration.Environment;
            telemetryLog.TrackEvent("information", "browser environment", browserEnv);

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
            if (_configuration == null)
                _configuration = LoadJSON(testPath);

            // cliff code
            string instrumentationKey = EnvVar("appInsightsInstrumentationKey");
            telemetryLog = new TelemetryLog(instrumentationKey);
            telemetryLog.TrackEvent("B2CTestDriver Started", "Test Environment", _configuration.TestConfiguration.Environment);

            string connectionString = EnvVar("blobStorageConnectionString");
            if(!string.IsNullOrEmpty(connectionString))
            {
                //useBlobStorage = true;
                blobFrame = new AzureBlobStorageFrame(
                    EnvVar("blobStorageConnectionString"),
                    EnvVar("blobStorageContainerName"));
            }

            
            var testSuite = new List<List<Page[]>>();

            foreach (var test in _configuration.Tests)
            {
                var jsonText = ReadFile(testPath + $"\\Tests\\{test}.json", useBlobStorage);
                if (String.IsNullOrEmpty(jsonText))
                {
                    telemetryLog.LogException("appsettings.json was not present or is empty.");
                    throw new Exception("appsettings.json was not present or is empty.");
                }
                else
                {
                    try
                    {
                        testSuite.Add(JsonConvert.DeserializeObject<List<Page[]>>(jsonText));
                    }
                    catch (Exception ex)
                    {
                        telemetryLog.TrackEvent("error", "invalid json", jsonText);
                        Assert.Fail($"invalid json: {jsonText}");
                    }
                }
            }
            

            for (int i = 0; i < testSuite.Count; i++)
            {
                yield return testSuite[i];
            }
        }

        [Test, TestCaseSource(nameof(TestStarter))]
        public async Task ExecuteFlow(List<Page[]> test)
        {
            TestContextWrite($"Execution of {_configuration.Tests[_testNumber++]}");
            for (int i = 0; i < test.Count; i++)
            {
                var pageActions = test[i];
                int j = 0;
                if (i == 0)
                {
                    // Start of new test, we need to navigate to the test start
                    if (pageActions[0].InputType == "navigation")
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
                                AssertFail($"Expected URL {pageActions[0].Value}, but current URL is {driver.Url}");
                            }
                            else if (String.IsNullOrEmpty(pageActions[0].Id))
                            {
                                AssertFail($"URL {pageActions[0].Id} did not load within the {_configuration.TestConfiguration.TimeOut} second time period.");
                            }
                            else
                            {
                                AssertFail($"URL {pageActions[0].Value} did not load a visible element {pageActions[0].Id} within the {_configuration.TestConfiguration.TimeOut} second time period.");
                            }
                        }
                        catch (Exception ex)
                        {
                            ExceptionMessage(ex);
                        }
                    }
                    else
                    {
                        AssertFail("Invalid test. There was no navigation to a page to start.");
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
                        AssertFail($"URL {pageActions[0].Value} did not load a visible element {pageActions[0].Id} within the {_configuration.TestConfiguration.TimeOut} second time period.");
                    }
                    catch (Exception ex)
                    {
                        ExceptionMessage(ex);
                    }
                }

                for (; j < test[i].Length; j++)
                {
                    if (!pageActions[j].InputType.StartsWith("Fn::") && pageActions[j].InputType != "successCheck")
                    {
                        // Check that the element we are looking for is visible
                        try
                        {
                            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(_configuration.TestConfiguration.TimeOut));
                            wait.Until(driver => driver.FindElement(By.Id(pageActions[j].Id)).Displayed);
                        }
                        catch (WebDriverTimeoutException)
                        {
                            AssertFail($"Next element {pageActions[j].Id} was not completed within the timeout period of {_configuration.TestConfiguration.TimeOut} second(s).");
                        }
                        catch (Exception ex)
                        {
                            ExceptionMessage(ex);
                        }
                    }
                    if (pageActions[j].InputType == "text")
                    {
                        driver.FindElement(By.Id(pageActions[j].Id)).SendKeys(pageActions[j].Value);
                    }
                    else if (pageActions[j].InputType == "button")
                    {
                        try
                        {
                            var buttonId = pageActions[j].Id;
                            driver.ExecuteJavaScript($"$('#{buttonId}').trigger('click')");
                        }
                        catch (JavaScriptException)
                        {
                            AssertFail($"Button with ID: {pageActions[j].Id} was not visible on the page.");
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
                            AssertFail($"Button with ID: {pageActions[j].Id} was not visible on the page.");
                        }
                    }
                    else if (pageActions[j].InputType == "dropdown")
                    {
                        try
                        {
                            driver.ExecuteJavaScript($"$('#{pageActions[j].Id}').val('{pageActions[j].Value}')");
                        }
                        catch (JavaScriptException)
                        {
                            AssertFail($"Dropdown with ID: {pageActions[j].Id} was not visible on the page.");
                        }
                    }
                    else if (pageActions[j].InputType == "checkbox")
                    {
                        try
                        {
                            if (pageActions[j].Value == "true")
                                driver.ExecuteJavaScript($"$('#{pageActions[j].Id}').attr('checked', true)");
                            else
                                driver.ExecuteJavaScript($"$('#{pageActions[j].Id}').attr('checked', false)");
                        }
                        catch (JavaScriptException)
                        {
                            AssertFail($"Checkbox with ID: {pageActions[j].Id} was not visible on the page.");
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
                                    AssertFail($"Next element {pageActions[0].Id} was not completed within the timeout period of {_configuration.TestConfiguration.TimeOut} second(s).");
                                }
                                var otpCode = await B2CMethods.GetEmailOTP(
                                    driver.FindElement(By.Id(pageActions[j].Id)).GetAttribute("value"),
                                    _keys["otpFunctionAppKey"], _keys["otpFunctionApp"],
                                    _configuration.TestConfiguration.OTP_Age);
                                driver.FindElement(By.Id("verificationCode")).SendKeys(otpCode);
                                break;
                            case "newRandomUser":
                                var newRandomUser = B2CMethods.NewRandomUser(pageActions[j].Value);
                                TestContextWrite($"New user ID: {newRandomUser}");
                                driver.FindElement(By.Id(pageActions[j].Id)).SendKeys(newRandomUser);
                                break;
                        }
                    }
                    else if (pageActions[j].InputType == "successCheck")
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
                            AssertFail($"URL {pageActions[j].Value} did not load within the {_configuration.TestConfiguration.TimeOut} second time period.");
                        }
                        catch (Exception ex)
                        {
                            ExceptionMessage(ex);
                        }

                        if (String.IsNullOrEmpty(pageActions[j].Id))
                        {
                            AssertPass($"Successfully landed on page: {pageActions[j].Value}");
                        }
                        else
                        {
                            AssertPass($"Successfully landed on page: {pageActions[j].Value} with element possessing ID: {pageActions[j].Id}");
                        }
                    }
                }
            }

            AssertFail("Test case logic failure or you forgot to terminate the test.");
        }


        public void AssertFail(string message)
        {
            telemetryLog.TrackMetric(telemetryMetricFail, 1);
            telemetryLog.TrackEvent("assert fail", "assertion", message);
            Assert.Fail(message);
        }


        public void AssertPass(string message)
        {
            telemetryLog.TrackMetric(telemetryMetricPass, 1);
            telemetryLog.TrackEvent("assert pass", "assertion", message);
            Assert.Pass(message);
        }

        
        public void ExceptionMessage(Exception ex)
        {
            telemetryLog.TrackMetric(telemetryMetricFail, 1);
            telemetryLog.TrackEvent("exception", "exception message", ex.ToString());
        }


        public void TestContextWrite(string message)
        {
            telemetryLog.TrackEvent("information", "message", message);
            TestContext.Write(message);
        }


        static string ReadFile(string path, bool isBlobFile = false)
        {
            string text = "";

            if (isBlobFile)
            {
                text = blobFrame.ReadAllText(path);
            }
            else
            {
                text = File.ReadAllText(path);
            }
            return text;
        }

        static string EnvVar(string key)
        {
            return Environment.GetEnvironmentVariable(key);
        }


        [OneTimeTearDown]
        public void TearDown()
        {
            telemetryLog.TrackEvent("B2CTestDriver finished", new Dictionary<string, string>());
            telemetryLog.Flush();

            driver.Quit();
        }
    }
}