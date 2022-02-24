using B2CTestDriver.methods;
using B2CTestDriver.models;
using Newtonsoft.Json;
using NUnit.Framework;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Support.Extensions;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
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
        internal static AppSettings LoadJSON()
        {
            AppSettings result = new AppSettings();
            using (StreamReader r = new StreamReader("appsettings.json"))
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
            using (StreamReader r = new StreamReader("keys.json"))
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

            if (browserEnv == "Chrome")
            {
                //Creates the ChomeDriver object, Executes tests on Google Chrome

                driver = new ChromeDriver(@".\drivers\");
            }
            else if (browserEnv == "Firefox")
            {
                // Specify Correct location of geckodriver.exe folder path. Ex: C:/Project/drivers

                driver = new FirefoxDriver(@".\drivers\");
            }
            else
            {
                throw new Exception("Unrecognized Browser Environment!");
            }
        }

        private static IEnumerable<List<Page[]>> TestStarter()
        {
            // TestCaseSource runs before OneTimeSetup
            if (_configuration == null)
                _configuration = LoadJSON();

            var testSuite = new List<List<Page[]>>();
            var result = new List<Page[]>();

            foreach (var page in _configuration.Pages)
            {
                if (result.Count != 0 && page[0].InputType == "navigation")
                {
                    testSuite.Add(result);
                    result = new List<Page[]>();
                }
                result.Add(page);
            }

            testSuite.Add(result);

            foreach (var test in testSuite)
            {
                yield return test;
            }
        }

        [Test, TestCaseSource(nameof(TestStarter))]
        public async Task ExecuteFlow(List<Page[]> test)
        {

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
                            Assert.Fail($"Next element {pageActions[j].Id} was not completed within the timeout period of {_configuration.TestConfiguration.TimeOut} second(s).");
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
                        else if (pageActions[j].InputType == "dropdown")
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
                                    var otpCode = await B2CMethods.GetEmailOTP(driver.FindElement(By.Id(pageActions[j].Id)).GetAttribute("value"), _keys["otpFunctionAppKey"], _keys["otpFunctionApp"]);
                                    driver.FindElement(By.Id("verificationCode")).SendKeys(otpCode);
                                    break;
                                case "newRandomUser":
                                    var newRandomUser = B2CMethods.NewRandomUser(pageActions[j].Value);
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

            Assert.Fail("My logic sucks or you forgot to terminate the test.");
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            driver.Quit();
        }
    }
}