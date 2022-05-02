﻿using System;
using System.Collections.Generic;
using System.Text.Json;
using System.IO;
using Tools;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Support.UI;
using OpenQA.Selenium.Support.Extensions;
using System.Linq;
using System.Windows;

namespace b2ctestcaserunner
{
    partial class TestCase
    {
        const string azureBlobSuitesDir = "testSuite";

        DateTime suiteStartTime = DateTime.Now;
        static string sessionUser = "testDriver" + DateTimeOffset.Now.ToUnixTimeSeconds();

        string workingDir = "";
        string driverPath = AppDomain.CurrentDomain.BaseDirectory;
        string exeBasePath = AppDomain.CurrentDomain.BaseDirectory;

        string instrumentationKey = "";
        Settings suiteSettings = null;
        TelemetryLog telemetryLog;
        IWebDriver webDriver;

        Dictionary<string, string> _keys;
        string currentTestName;
        string container;


        public TestCase(string settingsFile, string containerName)
        {
            container = containerName;
            LoadGlobals(settingsFile);
        }


        public void LoadGlobals(string settingsFile)
        {
            bool invalidJsonInSettingsFile = true;
            string appSettingsPath = string.IsNullOrEmpty(container)
                ? Path.Combine(workingDir, settingsFile)
                : $"{azureBlobSuitesDir}/{settingsFile}";  // azure blob storage

            try
            {
                string json = ReadFile(appSettingsPath);
                suiteSettings = JsonSerializer.Deserialize<Settings>(json);
                invalidJsonInSettingsFile = false;
            }
            catch ( Exception ex)
            {
            }

            string keysPath = Path.Combine(exeBasePath, "keys.json");
            _keys = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(keysPath));

            instrumentationKey = EnvVar("appInsightsInstrumentationKey");
            telemetryLog = new TelemetryLog(instrumentationKey);
            Console.WriteLine($"using instrumentation key {instrumentationKey}");

            telemetryLog.ConsoleLogger("--------------------------------------------------------");
            telemetryLog.TrackEvent("B2CTestDriver Started", "time", DateTime.Now.ToString());
            telemetryLog.TrackEvent("information", "browser", suiteSettings.TestConfiguration.Environment + "\n");

            if (invalidJsonInSettingsFile)
            {
                telemetryLog.ConsoleLogger("--------------------------------------------------------");
                telemetryLog.ConsoleLogger($"file {settingsFile} contains invalid json.  Test terminated");
                throw new Exception("suite file contains invalid json");
            }

            if (string.IsNullOrEmpty(suiteSettings.TestConfiguration.TimeOut)
                || (suiteSettings.TestConfiguration.timeOut == 0))
            {
                suiteSettings.TestConfiguration.timeOut = 5;

                telemetryLog.ConsoleLogger("----------------");
                telemetryLog.ConsoleLogger("Timeout value is set to zero or not defined.\tdefault value of 5 is used.");
                telemetryLog.ConsoleLogger(">>> Update the suite file, add a line \"    \"TimeOut\": \"5\"");
                telemetryLog.ConsoleLogger("----------------");
            }
        }


        public void SetupDriver()
        {
            string chromedriver = "chromedriver.exe";
            string firefoxdriver = "geckodriver.exe";

            string browser = suiteSettings.TestConfiguration.Environment;
            string name = "";
            bool throwException = false;

            try
            {
                switch (browser.ToLower())
                {
                    case "chrome":
                        if (!File.Exists(chromedriver))
                        {
                            name = chromedriver;
                            throw new Exception($"missing driver {chromedriver}");
                        }

                        name = "chrome.exe";

                        List<string> ls = new List<string>();       // idp-119 stop logging bluetooth driver missing
                        ls.Add("enable-logging"); 
                        ChromeOptions options = new ChromeOptions();
                        options.AddExcludedArguments(ls);

                        webDriver = new ChromeDriver(driverPath, options);      // chromedriver.exe           
                        break;
                    case "firefox":
                        if (!File.Exists(firefoxdriver))
                        {
                            name = firefoxdriver;
                            throw new Exception($"missing driver {firefoxdriver}");
                        }

                        name = "firefox.exe";
                        webDriver = new FirefoxDriver(driverPath);     // geckodriver.exe
                        int screenWidth = (int)SystemParameters.FullPrimaryScreenWidth;
                        webDriver.Manage().Window.Position = new System.Drawing.Point(screenWidth/3, 0); 
                        break;
                    default:
                        telemetryLog.TrackEvent("exception", "browser environment", "Unrecognized Browser Environment.  Test Aborted.");
                        throw new Exception("Unrecognized Browser Environment!");
                }
            }
            catch(Exception ex)
            {
                throwException = true;
                telemetryLog.ConsoleLogger("---------------------------");
                telemetryLog.ConsoleLogger($"cannot find {name}");
                telemetryLog.ConsoleLogger("---------------------------");
            }

            if(throwException)
            {
                try { if (webDriver != null) webDriver.Close(); } catch { }
                throw new Exception($"cannot find {name} for {browser}");
            }
        }


        public void Setup()
        {
            if (webDriver == null)
                SetupDriver();
            TelemetryLog.webDriver = webDriver;     // for screenshots
        }


        /// <summary>
        /// run the tests
        /// </summary>
        /// <param name="testName">optional:runs only a single test</param>
        public void DoTests(string testName = "")
        {
            if (suiteSettings.Tests.Length == 0)
            {
                telemetryLog.ConsoleLogger("----------------");
                telemetryLog.ConsoleLogger("No tests defined in Suite file to execute");
                telemetryLog.ConsoleLogger("----------------");
            }

            foreach (string test in suiteSettings.Tests)
            {
                if (string.IsNullOrEmpty(testName) || (test.ToLower() == testName.ToLower()))
                    ExecuteTest(test);
            }
        }

        public void ExecuteTest(string fileName)
        {
            DateTime testStartTime = DateTime.Now;
            try
            {
                currentTestName = fileName;

                string json = ReadFile(fileName);
                if (string.IsNullOrEmpty(json))
                {
                    telemetryLog.Flush();
                    return;
                }

                List<Page> pages = ParsePageJson(json, fileName);

                if (pages.Count == 0)
                {
                    telemetryLog.TrackEvent("File Failure", "Error", $"Invalid json found in file {fileName}");
                    telemetryLog.Flush();
                    return;
                }

                Setup();    // set up the environment

                Execute(pages, fileName);
            }
            catch (Exception e)
            {   // several failures throw exceptions
                TimeSpan elapsed = DateTime.Now - testStartTime;
                Dictionary<string, string> eventProperties = new Dictionary<string, string>();
                eventProperties.Add("Result", $"{currentTestName} : " + "Failure");
                eventProperties.Add("Test execution time", $"{elapsed.TotalSeconds:0.00}");
                telemetryLog.TrackEvent("TestFail", eventProperties);
                telemetryLog.TrackMetric(TelemetryLog.metricFail, 1);
            }

            Cleanup();
        }


        /// <summary>
        /// process the first page
        /// </summary>
        /// <param name="page"></param>
        /// <returns>true on success, false otherwise</returns>
        bool InitialPage(Page page)
        {
            bool isSuccess = false;

            if (page.inputType == "testCaseStart")
            {
                try
                {
                    webDriver.Navigate().GoToUrl(page.value);

                    var wait = new WebDriverWait(webDriver, TimeSpan.FromSeconds(suiteSettings.TestConfiguration.timeOut));

                    // If no ID we just want to check for URL
                    if (String.IsNullOrEmpty(page.id))
                        wait.Until(webDriver => webDriver.Url.Contains(page.value));
                    else
                    {
                        wait.Until(webDriver => webDriver.Url.Contains(page.value) && webDriver.FindElements(By.Id(page.id)).Count > 0);
                    }
                    isSuccess = true;
                }
                catch (WebDriverTimeoutException)
                {
                    if (!webDriver.Url.Contains(page.value))
                    {
                        telemetryLog.TrackEvent("URL Failure", "Error", $"Test {currentTestName}: Expected URL {page.value}, but current URL is {webDriver.Url}");
                        telemetryLog.ConsoleLogger($"-------------\n>>> FIX THE URL {page.value}\n-------------");
                    }
                    else if (String.IsNullOrEmpty(page.id))
                    {
                        telemetryLog.TrackEvent("Timeout Failure", "Error", $"Test {currentTestName}: URL {page.id} did not load within the {suiteSettings.TestConfiguration.TimeOut} second time period.");
                    }
                    else
                    {
                        telemetryLog.TrackEvent("Visible Element", "Error", $"Test {currentTestName}: URL {page.value} did not load a visible element {page.id} within the {suiteSettings.TestConfiguration.TimeOut} second time period.");
                        telemetryLog.ConsoleLogger($"-------------\n>>> FIX THE ELEMENT NAME {page.id}\n-------------");
                    }
                    throw new Exception("WebDriver Timeout Exception");
                }
                catch (Exception ex)
                {
                    telemetryLog.TrackEvent("Exception Thrown", "Exception", ex.ToString());
                    throw ex;
                }
            }
            else
            {
                telemetryLog.TrackEvent("Invalid test", "Invalid test", $"{currentTestName}: Invalid test. There was no navigation to a page to start.");
            }

            return isSuccess;
        }


        public void Execute(List<Page> pages, string currentTestName)
        {
            bool testSuccess = false;
            DateTime testStartTime = DateTime.Now;

            string prevURL = "";
            string emailAddress = "";
            telemetryLog.TrackEvent("Test Started", "Test Name", currentTestName);
            if(!string.IsNullOrEmpty(container))
                telemetryLog.TrackEvent("BlobStorage", "Container Name", container);

            int iStart = 0;
            for (int i = 0; i < pages.Count; i++)
            {
                if (pages[i].inputType == "testCaseStart")
                {
                    iStart = i;
                    break;
                }
            }

            if (!InitialPage(pages[iStart]))
            {
                return;
            }

            for (int i = iStart + 1; i < pages.Count; i++)
            {
                Page page = pages[i];
                if (page.inputType == "metadata")
                {
                    continue;     // ignore metadata
                }

                // Otherwise new page and we need to check if first element we want to interact with is loaded
                if (page.inputType == "Navigate")
                {
                    try
                    {
                        while (webDriver.Url == prevURL)
                            System.Threading.Thread.Sleep(100);

                        webDriver.Navigate().GoToUrl(page.value);

                        var wait = new WebDriverWait(webDriver, TimeSpan.FromSeconds(suiteSettings.TestConfiguration.timeOut));
                        wait.Until(webDriver => webDriver.Url.Contains(page.value));
                    }
                    catch (WebDriverTimeoutException)
                    {
                        if (!webDriver.Url.Contains(page.value))
                        {
                            telemetryLog.TrackEvent("Url Failure", "Error", $"Test {currentTestName}: Expected URL {page.value}, but current URL is {webDriver.Url}");
                            throw new Exception("Test Failure");
                        }
                        else
                        {
                            telemetryLog.TrackEvent("Timeout Failure", "Error", $"Test {currentTestName}: URL {page.id} did not load within the {suiteSettings.TestConfiguration.TimeOut} second time period.");
                            throw new Exception("Test Failure");
                        }
                    }
                    catch (Exception ex)
                    {
                        telemetryLog.TrackException(ex);
                    }
                    continue;
                }
                prevURL = webDriver.Url;

                try
                {
                    var wait = new WebDriverWait(webDriver, TimeSpan.FromSeconds(suiteSettings.TestConfiguration.timeOut));
                    wait.Until(webDriver => webDriver.FindElements(By.Id(page.id)).Count > 0);
                }
                catch (WebDriverTimeoutException)
                {
                    var properties = new Dictionary<string, string>()
                    {
                        { "Test Name", currentTestName},
                        { "URL", page.value },
                        { "Error", "Web Driver Timeout" }
                    };
                    telemetryLog.TrackEvent("Test Failure", "Error", "WebDriver Timeout");
                    telemetryLog.ConsoleLogger("This usually happens with the incorrect input values, please refer the screenshot to find out what could have been wrong");
                    throw new Exception("Test Failure");
                }
                catch (Exception ex)
                {
                    telemetryLog.TrackEvent("Exception Thrown", "exception", ex.ToString());
                    throw new Exception(ex.ToString());
                }


                if (!page.inputType.StartsWith("Fn::") && page.inputType != "testCaseComplete")
                {
                    // Check that the element we are looking for is visible
                    try
                    {
                        var wait = new WebDriverWait(webDriver, TimeSpan.FromSeconds(suiteSettings.TestConfiguration.timeOut));
                        wait.Until(driver => driver.FindElement(By.Id(page.id)).Displayed);
                    }
                    catch (WebDriverTimeoutException)
                    {
                        telemetryLog.TrackEvent("Timeout Failure", "Error", $"Test {currentTestName}: Next element {page.id} was not completed within the timeout period of {suiteSettings.TestConfiguration.TimeOut} second(s).");
                        throw new Exception("WebDriver Timeout Exception");
                    }
                    catch (Exception ex)
                    {
                        telemetryLog.TrackException(ex);
                        throw ex;
                    }
                }
                if (page.inputType == "Text")
                {
                    try
                    {
                        // use javascript to clear the field in case there is already text present, still want to use send keys so as to trigger any attached events
                        webDriver.ExecuteJavaScript($"$('#{page.id}').val('')");
                        webDriver.FindElement(By.Id(page.id)).SendKeys(page.value);
                    }
                    catch (JavaScriptException jse)
                    {
                        telemetryLog.TrackEvent("Input failure", "Error", $"Test {currentTestName}: Input text field with ID: {page.id} was not visible on the page.");
                        throw jse;
                    }
                    catch (Exception ex)
                    {
                        telemetryLog.TrackException(ex);
                        throw ex;
                    }
                }
                else if (page.inputType == "Button")
                {
                    try
                    {
                        webDriver.FindElement(By.Id(page.id)).Click();
                    }
                    catch (JavaScriptException jse)
                    {
                        telemetryLog.TrackEvent("Button Failure", "Error", $"Test {currentTestName}: Button with ID: {page.id} was not visible on the page.");
                        throw jse;
                    }
                    catch (Exception ex)
                    {
                        telemetryLog.TrackException(ex);
                        throw ex;
                    }
                }
                else if (page.inputType == "Dropdown")
                {
                    try
                    {
                        SelectElement valueSelector = new SelectElement(webDriver.FindElement(By.Id(page.id)));
                        valueSelector.SelectByValue(page.value);
                    }
                    catch (JavaScriptException jse)
                    {
                        telemetryLog.TrackEvent("Dropdown Failure", "Error", $"Dropdown with ID: {page.id} was not visible on the page.");
                        throw jse;
                    }
                }
                else if (page.inputType == "Checkbox")
                {
                    try
                    {
                        webDriver.ExecuteJavaScript($"$('#{page.id}').trigger('click')");
                    }
                    catch (JavaScriptException jse)
                    {
                        telemetryLog.TrackEvent("Checkbox Failure", "Error", $"Test {currentTestName}: Checkbox with ID: {page.id} was not visible on the page.");
                        throw jse;
                    }
                }
                else if (page.inputType.Contains("Fn::"))
                {
                    var fnName = page.inputType.Substring(4);
                    switch (fnName)
                    {
                        case "otpEmail":
                            try
                            {
                                var wait = new WebDriverWait(webDriver, TimeSpan.FromSeconds(suiteSettings.TestConfiguration.timeOut));
                                wait.Until(driver => driver.FindElement(By.Id("emailVerificationControl_but_verify_code")).Displayed);
                            }
                            catch (WebDriverTimeoutException)
                            {
                                telemetryLog.TrackEvent("Timeout Failure", "Error", $"Test {currentTestName}: Next element emailVerificationControl_but_verify_code was not completed within the timeout period of {suiteSettings.TestConfiguration.TimeOut} second(s).");
                                throw new Exception("Test Failure");
                            }
                            if (page.id == "" && page.value == "")
                            {
                                telemetryLog.TrackEvent("Test Failure", "Error", $"Test {currentTestName}: otpEmail function requires an id and a value.");
                                throw new Exception("Test Failure");
                            }
                            else if (page.id == "")
                            {
                                telemetryLog.TrackEvent("Test Failure", "Error", $"Test {currentTestName}: otpEmail function requires an id.");
                                throw new Exception("Test Failure");
                            }
                            else if (page.value == "")
                            {
                                telemetryLog.TrackEvent("Test Failure", "Error", $"Test {currentTestName}: otpEmail function requires a value.");
                                throw new Exception("Test Failure");
                            }

                            string value = webDriver.FindElement(By.Id(page.id)).GetAttribute("value");
                            var otpCode = B2CMethods.GetEmailOTP(
                                value,
                                _keys["otpFunctionAppKey"], _keys["otpFunctionApp"],
                                suiteSettings.TestConfiguration.OTP_Age).Result;
                            try
                            {
                                webDriver.FindElement(By.Id(page.value)).SendKeys(otpCode);
                            }
                            catch (NoSuchElementException)
                            {
                                telemetryLog.TrackEvent("Test Failure", "Error", $"Test {currentTestName}: otpEmail function value does not match the id of a visible element on the page.");
                                throw new Exception("Test Failure");
                            }
                            break;
                        case "newRandomUser":
                            emailAddress = B2CMethods.NewRandomUser(page.value);
                            telemetryLog.TrackEvent("information", "New User", $"Test {currentTestName}: New user ID: {emailAddress}");
                            webDriver.FindElement(By.Id(page.id)).SendKeys(emailAddress);
                            break;
                        case "sessionUser":
                            emailAddress = sessionUser + page.value;
                            webDriver.FindElement(By.Id(page.id)).SendKeys(emailAddress);
                            break;
                    }
                }
                else if (page.inputType == "testCaseComplete")
                {
                    try
                    {
                        var wait = new WebDriverWait(webDriver, TimeSpan.FromSeconds(suiteSettings.TestConfiguration.timeOut));
                        // If no ID we just want to check for URL (preference is to also look for an id)
                        if (String.IsNullOrEmpty(page.id))
                            wait.Until(webDriver => webDriver.Url.Contains(page.value));
                        else
                        {
                            wait.Until(webDriver => webDriver.Url.Contains(page.value) && webDriver.FindElements(By.Id(page.id)).Count > 0);
                        }

                        string suffix = string.IsNullOrEmpty(page.id) ? $"" : $"with element possessing ID: {page.id}";
                        telemetryLog.TrackEvent("information", "TestCaseComplete", $"Test {currentTestName}: Successfully landed on page: {page.value} {suffix}");
                        testSuccess = true;
                    }
                    catch (WebDriverTimeoutException)
                    {
                        telemetryLog.TrackEvent("Timeout Failure", "Error", $"Test {currentTestName}: URL {page.value} did not load within the {suiteSettings.TestConfiguration.TimeOut} second time period.");
                        throw new Exception("Test Failure");
                    }
                    catch (Exception ex)
                    {
                        telemetryLog.TrackException(ex);
                        throw new Exception("Test Failure");
                    }

                }
            }

            if (suiteSettings.DebugMode.GetValueOrDefault(false))
            {
                System.Threading.Thread.Sleep(suiteSettings.TestConfiguration.DebugWait.GetValueOrDefault(3) * 1000);
            }

            if (pages.Where(p => p.inputType == "testCaseComplete").FirstOrDefault() == null)
            {
                telemetryLog.ConsoleLogger("-------------------");
                telemetryLog.ConsoleLogger($">>> test file should include a line       \"inputType\": \"testCaseComplete\",");
                telemetryLog.ConsoleLogger("-------------------");
                telemetryLog.TrackEvent("Test Failure", "Error", "Test case logic failure or you forgot to terminate the test.");
            }

            TimeSpan elapsed = DateTime.Now - testStartTime;
            Dictionary<string, string> eventProperties = new Dictionary<string, string>();
            eventProperties.Add("Result", $"{currentTestName} : " + (testSuccess ? "Success":"Failure"));
            eventProperties.Add("Test execution time", $"{elapsed.TotalSeconds:0.00}");
            telemetryLog.TrackEvent(testSuccess ? "TestPass" : "TestFail", eventProperties);
            telemetryLog.TrackMetric(testSuccess ? TelemetryLog.metricPass : TelemetryLog.metricFail, 1);
        }


        public void Cleanup()
        {
            TimeSpan elapsed = DateTime.Now - suiteStartTime;
            string msg = $"{elapsed.TotalSeconds:0.00} seconds";
            telemetryLog.TrackEvent("Test Suite Completed", "Suite execution time", msg);
            telemetryLog.Flush();

            if (webDriver != null)
            {
                webDriver.Quit();
                webDriver = null;
            }
        }
    }
}
