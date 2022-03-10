using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.IO;
using Tools;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Firefox;
using System.Threading.Tasks;
using OpenQA.Selenium.Support.UI;
using OpenQA.Selenium.Support.Extensions;
using System.Linq;

namespace b2ctestcaserunner
{
    partial class TestCase
    {
        const string telemetryMetricPass = "Pass";
        const string telemetryMetricFail = "Fail";

        string workingDir = "";
        string driverPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"drivers");
        string exeBasePath = AppDomain.CurrentDomain.BaseDirectory;

        string instrumentationKey = "";
        AppSettings appSettings = null;
        TelemetryLog telemetryLog;
        IWebDriver driver;

        Dictionary<string, string> _keys;
        string currentTestName;

        public TestCase()
        {

        }


        public void LoadGlobals()
        {
            string appSettingsPath = EnvVar("appsettings.json", Path.Combine(workingDir, "appsettings.json"));
            appSettings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(appSettingsPath));

            string keysPath = Path.Combine(exeBasePath, "keys.json");
            _keys = JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(keysPath));

            instrumentationKey = EnvVar("appInsightsInstrumentationKey");
            telemetryLog = new TelemetryLog(instrumentationKey);
            telemetryLog.TrackEvent("------------------\nB2CTestDriver Started", "time", DateTime.Now.ToString());
        }


        public void SetupDriver()
        {
            string browser = appSettings.TestConfiguration.Environment;
            telemetryLog.TrackEvent("information", "browser", browser);

            switch (browser)
            {
                case "Chrome":
                    driver = new ChromeDriver(driverPath);      // chromedriver.exe
                    break;
                case "FireFox":
                    driver = new FirefoxDriver(driverPath);     // geckodriver.exe
                    break;
                default:
                    telemetryLog.TrackEvent("exception", "browser environment", "Unrecognized Browser Environment.  Test Aborted.");
                    throw new Exception("Unrecognized Browser Environment!");
            }
        }
    

        public void Setup()
        {
            if (appSettings == null)
                LoadGlobals();

            if (driver == null) 
                SetupDriver();
        }


        public void ExecuteTest(string fileName)
        {
            try
            {
                currentTestName = fileName;

                string json = ReadFile(fileName);
                List<Page> pages = ParsePageJson(json);

                Setup();    // set up the environment

                Execute(pages, fileName);
            }
            catch { }

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

            // Start of new test, we need to navigate to the test start
            if (page.inputType == "testCaseStart")
            {
                // Increment j as we are handling the first element
                try
                {
                    driver.Navigate().GoToUrl(page.value);

                    var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(appSettings.TestConfiguration.timeOut));
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
                    if (!driver.Url.Contains(page.value))
                    {
                        telemetryLog.TrackEvent("URL Failure", "Error", $"Test {currentTestName}: Expected URL {page.value}, but current URL is {driver.Url}");
                    }
                    else if (String.IsNullOrEmpty(page.id))
                    {
                        telemetryLog.TrackEvent("Timeout Failure", "Error", $"Test {currentTestName}: URL {page.id} did not load within the {appSettings.TestConfiguration.TimeOut} second time period.");
                    }
                    else
                    {
                        telemetryLog.TrackEvent("Visible Element", "Error", $"Test {currentTestName}: URL {page.value} did not load a visible element {page.id} within the {appSettings.TestConfiguration.TimeOut} second time period.");
                    }
                }
                catch (Exception ex)
                {
                    telemetryLog.TrackEvent("Exception Thrown", "Exception", ex.ToString());
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
            telemetryLog.TrackEvent("Test Started", "Test Name", currentTestName);

            if(!InitialPage(pages[0]))
            {
                return;
            }

            for (int i = 1; i < pages.Count; i++)
            {
                Page page = pages[i];

                // Otherwise new page and we need to check if first element we want to interact with is loaded
                try
                {
                    var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(appSettings.TestConfiguration.timeOut));
                    wait.Until(webDriver => webDriver.FindElements(By.Id(page.id)).Count > 0);
                }
                catch (WebDriverTimeoutException)
                {
                    var properties = new Dictionary<string, string>()
                    {
                        { "Test Name", currentTestName},
                        { "URL", page.value }
                    };

                    telemetryLog.TrackEvent("Test Failure", properties);
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
                        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(appSettings.TestConfiguration.timeOut));
                        wait.Until(driver => driver.FindElement(By.Id(page.id)).Displayed);
                    }
                    catch (WebDriverTimeoutException)
                    {
                        telemetryLog.TrackEvent("Timeout Failure", "Error", $"Test {currentTestName}: Next element {page.id} was not completed within the timeout period of {appSettings.TestConfiguration.TimeOut} second(s).");
                    }
                    catch (Exception ex)
                    {
                        telemetryLog.TrackException(ex);
                    }
                }
                if (page.inputType == "Text")
                {
                    try
                    {
                        // use javascript to clear the field in case there is already text present, still want to use send keys so as to trigger any attached events
                        driver.ExecuteJavaScript($"$('#{page.id}').val('')");
                        driver.FindElement(By.Id(page.id)).SendKeys(page.value);
                    }
                    catch (JavaScriptException)
                    {
                        telemetryLog.TrackEvent("Input failure", "Error", $"Test {currentTestName}: Input text field with ID: {page.id} was not visible on the page.");
                    }
                    catch (Exception ex)
                    {
                        telemetryLog.TrackException(ex);
                    }
                }
                else if (page.inputType == "Button")
                {
                    try
                    {
                        driver.FindElement(By.Id(page.id)).Click();
                    }
                    catch (JavaScriptException)
                    {
                        telemetryLog.TrackEvent("Button Failure", "Error", $"Test {currentTestName}: Button with ID: {page.id} was not visible on the page.");
                    }
                    catch (Exception ex)
                    {
                        telemetryLog.TrackException(ex);
                    }
                }
                else if (page.inputType == "Dropdown")
                {
                    try
                    {
                        SelectElement valueSelector = new SelectElement(driver.FindElement(By.Id(page.id)));
                        valueSelector.SelectByValue(page.value);
                    }
                    catch (JavaScriptException)
                    {
                        telemetryLog.TrackEvent("Dropdown Failure", "Error", $"Dropdown with ID: {page.id} was not visible on the page.");
                    }
                }
                else if (page.inputType == "Checkbox")
                {
                    try
                    {
                        driver.ExecuteJavaScript($"$('#{page.id}').trigger('click')");
                    }
                    catch (JavaScriptException)
                    {
                        telemetryLog.TrackEvent("Checkbox Failure", "Error", $"Test {currentTestName}: Checkbox with ID: {page.id} was not visible on the page.");
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
                                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(appSettings.TestConfiguration.timeOut));
                                wait.Until(driver => driver.FindElement(By.Id("emailVerificationControl_but_verify_code")).Displayed);
                            }
                            catch (WebDriverTimeoutException)
                            {
                                telemetryLog.TrackEvent("Timeout Failure", "Error", $"Test {currentTestName}: Next element emailVerificationControl_but_verify_code was not completed within the timeout period of {appSettings.TestConfiguration.TimeOut} second(s).");
                            }
                            if (page.id == "" && page.value == "")
                            {
                                telemetryLog.TrackEvent("Test Failure", "Error", $"Test {currentTestName}: otpEmail function requires an id and a value.");
                            }
                            else if (page.id == "")
                            {
                                telemetryLog.TrackEvent("Test Failure", "Error", $"Test {currentTestName}: otpEmail function requires an id.");
                            }
                            else if (page.value == "")
                            {
                                telemetryLog.TrackEvent("Test Failure", "Error", $"Test {currentTestName}: otpEmail function requires a value.");
                            }
                            var otpCode = B2CMethods.GetEmailOTP(
                                driver.FindElement(By.Id(page.id)).GetAttribute("value"),
                                _keys["otpFunctionAppKey"], _keys["otpFunctionApp"],
                                appSettings.TestConfiguration.OTP_Age).Result;
                            try
                            {
                                driver.FindElement(By.Id(page.value)).SendKeys(otpCode);
                            }
                            catch (NoSuchElementException)
                            {
                                telemetryLog.TrackEvent("Test Failure", "Error", $"Test {currentTestName}: otpEmail function value does not match the id of a visible element on the page.");
                            }
                            break;
                        case "newRandomUser":
                            var newRandomUser = B2CMethods.NewRandomUser(page.value);
                            telemetryLog.TrackEvent("information", "New User", $"Test {currentTestName}: New user ID: {newRandomUser}");
                            driver.FindElement(By.Id(page.id)).SendKeys(newRandomUser);
                            break;
                    }
                }
                else if (page.inputType == "Navigate")
                {
                    try
                    {
                        driver.Navigate().GoToUrl(page.value);

                        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(appSettings.TestConfiguration.timeOut));
                        wait.Until(webDriver => webDriver.Url.Contains(page.value));
                    }
                    catch (WebDriverTimeoutException)
                    {
                        if (!driver.Url.Contains(page.value))
                        {
                            telemetryLog.TrackEvent("Url Failure", "Error", $"Test {currentTestName}: Expected URL {page.value}, but current URL is {driver.Url}");
                        }
                        else
                        {
                            telemetryLog.TrackEvent("Timeout Failure", "Error", $"Test {currentTestName}: URL {page.id} did not load within the {appSettings.TestConfiguration.TimeOut} second time period.");
                        }
                    }
                    catch (Exception ex)
                    {
                        telemetryLog.TrackException(ex);
                    }
                }
                else if (page.inputType == "testCaseComplete")
                {
                    try
                    {
                        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(appSettings.TestConfiguration.timeOut));
                        // If no ID we just want to check for URL (preference is to also look for an id)
                        if (String.IsNullOrEmpty(page.id))
                            wait.Until(webDriver => webDriver.Url.Contains(page.value));
                        else
                        {
                            wait.Until(webDriver => webDriver.Url.Contains(page.value) && webDriver.FindElements(By.Id(page.id)).Count > 0);
                        }
                    }
                    catch (WebDriverTimeoutException)
                    {
                        telemetryLog.TrackEvent("Timeout Failure", "Error", $"Test {currentTestName}: URL {page.value} did not load within the {appSettings.TestConfiguration.TimeOut} second time period.");
                    }
                    catch (Exception ex)
                    {
                        telemetryLog.TrackException(ex);
                    }

                    string suffix = string.IsNullOrEmpty(page.id) ? $"" : $"with element possessing ID: {page.id}";
                    telemetryLog.TrackEvent("information", "success", $"Test {currentTestName}: Successfully landed on page: {page.value} {suffix}");
                }
            }

            if (pages.Where(p => p.inputType == "testCaseComplete").FirstOrDefault() == null)
            {
                telemetryLog.TrackEvent("Test Failure", "Error", "Test completion not configured.");
            }
        }


        public void Cleanup()
        {
            telemetryLog.TrackEvent("B2CTestDriver Completed", "time", $"{DateTime.Now}");
            telemetryLog.Flush();

            if (driver != null)
                driver.Quit();
        }
    }
}
