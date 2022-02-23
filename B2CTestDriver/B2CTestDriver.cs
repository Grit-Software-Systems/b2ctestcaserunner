using LoadTest.models;
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

namespace B2CPortalTest
{
    [TestFixture]
    public class B2CTestDriver
    {
        IWebDriver driver;
        private static AppSettings Configuration;

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

        internal static void GetEmailOTP()
        {
            System.Diagnostics.Debug.WriteLine("Get OTP Email");
        }

        internal static void GetEmailOTP(string email)
        {
            System.Diagnostics.Debug.WriteLine("Get OTP Email");
        }

        [OneTimeSetUp]
        public void SetUp()
        {
            //Below code is to get the drivers folder path dynamically.

            //You can also specify chromedriver.exe path dircly ex: C:/MyProject/Project/drivers

            var browserEnv = Configuration.TestConfiguration.Environment;

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
            if (Configuration == null)
                Configuration = LoadJSON();

            var testSuite = new List<List<Page[]>>();
            var result = new List<Page[]>();

            foreach (var page in Configuration.Pages)
            {
                if (result.Count != 0 && page[0].InputType == "navigation")
                {
                    testSuite.Add(result);
                    result = new List<Page[]>();
                }
                    result.Add(page);
            }

            testSuite.Add(result);

            foreach(var test in testSuite)
            {
                yield return test;
            }
        }

        [Test, TestCaseSource(nameof(TestStarter))]
        public void ExecuteFlow(List<Page[]> test)
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
                            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(Configuration.TestConfiguration.TimeOut));

                            // If no ID we just want to check for URL
                            if (String.IsNullOrEmpty(pageActions[0].Id))
                                wait.Until(driver => driver.Url.Contains(pageActions[0].Value));
                            else
                            {
                                // Function to check for URL and element with supplied ID
                                Func<IWebDriver, bool> waitForElement = new Func<IWebDriver, bool>((IWebDriver Web) =>
                                {
                                    if (driver.Url.Contains(pageActions[0].Value))
                                    {
                                        try
                                        {
                                            Web.FindElement(By.Id(pageActions[0].Id));
                                        }
                                        catch (NoSuchElementException)
                                        {
                                            return false;
                                        }

                                        return true;
                                    }
                                    return false;
                                });


                                wait.Until(waitForElement);
                            }
                        }
                        catch (WebDriverTimeoutException)
                        {
                            if (String.IsNullOrEmpty(pageActions[0].Id))
                            {
                                Assert.Fail($"URL {pageActions[0].Value} did not load within the {Configuration.TestConfiguration.TimeOut} second time period.");
                            }
                            else
                            {
                                Assert.Fail($"URL {pageActions[0].Value} did not load a visible element {pageActions[0].Id} within the {Configuration.TestConfiguration.TimeOut} second time period.");
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
                    // New page, check that the element we are looking for exists
                    try
                    {
                        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(Configuration.TestConfiguration.TimeOut));
                        wait.Until(driver => driver.FindElement(By.Id(pageActions[0].Id)));
                    }
                    catch (WebDriverTimeoutException)
                    {
                        Assert.Fail($"Next element {pageActions[0].Id} was not completed within the timeout period of {Configuration.TestConfiguration.TimeOut} second(s).");
                    }
                }
                for (; j < test[i].Length; j++)
                {
                    if (pageActions[j].InputType == "text")
                    {
                        driver.FindElement(By.Id(pageActions[j].Id)).SendKeys(pageActions[j].Value);
                    }
                    else if (pageActions[j].InputType == "button")
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
                                if (String.IsNullOrEmpty(pageActions[j].Value))
                                    GetEmailOTP();
                                else
                                    GetEmailOTP(pageActions[j].Value);
                                break;
                        }
                    }
                    else if (pageActions[j].InputType == "successCheck")
                    {
                        try
                        {
                            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(Configuration.TestConfiguration.TimeOut));

                            // If no ID we just want to check for URL
                            if (String.IsNullOrEmpty(pageActions[j].Id))
                                wait.Until(driver => driver.Url.Contains(pageActions[j].Value));
                            else
                            {
                                // Function to check for URL and element with supplied ID
                                Func<IWebDriver, bool> waitForElement = new Func<IWebDriver, bool>((IWebDriver Web) =>
                                {
                                    if (driver.Url.Contains(pageActions[j].Value))
                                    {
                                        try
                                        {
                                            Web.FindElement(By.Id(pageActions[j].Id));
                                        }
                                        catch (NoSuchElementException)
                                        {
                                            return false;
                                        }
                                        return true;
                                    }
                                    return false;
                                });


                                wait.Until(waitForElement);
                            }
                        }
                        catch (WebDriverTimeoutException)
                        {
                            Assert.Fail($"URL {pageActions[j].Value} did not load within the {Configuration.TestConfiguration.TimeOut} second time period.");
                        }

                        if (String.IsNullOrEmpty(pageActions[j].Id))
                        {
                            Assert.Pass($"Successfully landed on page: {pageActions[j].Value} with element possessing ID: {pageActions[j].Id}");
                        }
                        else
                        {
                            Assert.Pass($"Successfully landed on page: {pageActions[j].Value}");
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