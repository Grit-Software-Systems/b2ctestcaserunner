using B2CTestDriver.methods;
using B2CTestDriver.models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
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

namespace B2CTestDriver
{
    [TestFixture]
    public class B2CTestDriver
    {
        IWebDriver driver;
        private static AppSettings _configuration;
        private static Dictionary<string, string> _keys;
        private static int _testNumber = 0;
        internal static AppSettings LoadJSON(string path)
        {
            AppSettings result = new AppSettings();
            using (StreamReader r = new StreamReader(path + @"\appsettings.json"))
            {
                var jsonText = r.ReadToEnd();
                if (String.IsNullOrEmpty(jsonText))
                {
                    throw new Exception("appsettings.json was not present or is empty.");
                }
                else
                {
                    result = JsonConvert.DeserializeObject<AppSettings>(jsonText);
                }
            }

            return result;
        }

        [OneTimeSetUp]
        public void SetUp()
        {
            var keysPath = TestContext.CurrentContext.WorkDirectory;
            using (StreamReader r = new StreamReader(keysPath + @"\keys.json"))
            {
                var jsonText = r.ReadToEnd();
                if (!String.IsNullOrEmpty(jsonText))
                {
                    _keys = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonText);
                }
            }
            //Below code is to get the drivers folder path dynamically.

            //You can also specify chromedriver.exe path dircly ex: C:/MyProject/Project/drivers

            var browserEnv = _configuration.TestConfiguration.Environment;

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
                throw new Exception("Unrecognized Browser Environment!");
            }
        }

        private static IEnumerable<List<Page[]>> TestStarter()
        {
            var testPath = TestContext.CurrentContext.WorkDirectory;
            // TestCaseSource runs before OneTimeSetup
            if (_configuration == null)
                _configuration = LoadJSON(testPath);

            var testSuite = new List<List<Page[]>>();

            foreach (var test in _configuration.Tests)
            {
                using (StreamReader r = new StreamReader(testPath + $"\\Tests\\{test}.json"))
                {
                    var jsonText = r.ReadToEnd();
                    if (String.IsNullOrEmpty(jsonText))
                    {
                        throw new Exception("appsettings.json was not present or is empty.");
                    }
                    else
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
                }
            }

            foreach(List<Page[]> testFlow in testSuite)
            {
                yield return testFlow;
            }
        }

        [Test, TestCaseSource(nameof(TestStarter))]
        public async Task ExecuteFlow(List<Page[]> test)
        {
            TestContext.Write($"Execution of {_configuration.Tests[_testNumber++]}");
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
                        driver.Navigate().GoToUrl(pageActions[0].Value);
                        try
                        {
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
                                Assert.Fail($"Expected URL {pageActions[0].Value}, but current URL is {driver.Url}");
                            }
                            else if (String.IsNullOrEmpty(pageActions[0].Id))
                            {
                                Assert.Fail($"URL {pageActions[0].Id} did not load within the {_configuration.TestConfiguration.TimeOut} second time period.");
                            }
                            else
                            {
                                Assert.Fail($"URL {pageActions[0].Value} did not load a visible element {pageActions[0].Id} within the {_configuration.TestConfiguration.TimeOut} second time period.");
                            }
                        }
                    }
                    else
                    {
                        Assert.Fail("Invalid test. There was no navigation to a page to start.");
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
                        Assert.Fail($"URL {pageActions[0].Value} did not load a visible element {pageActions[0].Id} within the {_configuration.TestConfiguration.TimeOut} second time period.");
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
                            Assert.Fail($"Next element {pageActions[j].Id} was not completed within the timeout period of {_configuration.TestConfiguration.TimeOut} second(s).");
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
                            Assert.Fail($"Button with ID: {pageActions[j].Id} was not visible on the page.");
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
                            Assert.Fail($"Button with ID: {pageActions[j].Id} was not visible on the page.");
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
                            Assert.Fail($"Dropdown with ID: {pageActions[j].Id} was not visible on the page.");
                        }
                    }
                    else if (pageActions[j].InputType == "Checkbox")
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
                            Assert.Fail($"Checkbox with ID: {pageActions[j].Id} was not visible on the page.");
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
                                    Assert.Fail($"Next element {pageActions[0].Id} was not completed within the timeout period of {_configuration.TestConfiguration.TimeOut} second(s).");
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
                                TestContext.Write($"New user ID: {newRandomUser}");
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
                            Assert.Fail($"URL {pageActions[j].Value} did not load within the {_configuration.TestConfiguration.TimeOut} second time period.");
                        }

                        if (String.IsNullOrEmpty(pageActions[j].Id))
                        {
                            Assert.Pass($"Successfully landed on page: {pageActions[j].Value}");
                        }
                        else
                        {
                            Assert.Pass($"Successfully landed on page: {pageActions[j].Value} with element possessing ID: {pageActions[j].Id}");
                        }
                    }
                }
            }

            Assert.Fail("Test completion not configured.");
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            driver.Quit();
        }
    }
}