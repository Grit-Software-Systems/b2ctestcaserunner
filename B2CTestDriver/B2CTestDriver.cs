using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Support.UI;
using OpenQA.Selenium.Support.Extensions;
using System;
using System.Collections.ObjectModel;
using System.IO;
using LoadTest.models;
using Newtonsoft.Json;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace B2CPortalTest
{
    [TestFixture]
    public class B2CTestDriver
    {
        IWebDriver driver;
        private readonly AppSettings Configuration = LoadJSON();

        internal static AppSettings LoadJSON()
        {
            var serializer = new JsonSerializer();
            AppSettings result = new AppSettings();
            using (StreamReader r = new StreamReader("appsettings.json"))
            {
                var jsonText = r.ReadToEnd();
                Console.WriteLine(jsonText);
               result = JsonConvert.DeserializeObject<AppSettings>(jsonText);
            }

            return result;
        }

        internal static void GetEmailOTP()
        {
            System.Diagnostics.Debug.WriteLine("Get OTP Email");
        }

        [OneTimeSetUp]
        public void SetUp()
        {
            //Below code is to get the drivers folder path dynamically.

            //You can also specify chromedriver.exe path dircly ex: C:/MyProject/Project/drivers

            string path = Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName;

            var browserEnv = Configuration.TestConfiguration.Environment;

            if (browserEnv == "Chrome")
            {
                //Creates the ChomeDriver object, Executes tests on Google Chrome

                driver = new ChromeDriver(path + @"\drivers\");
            }
            else if (browserEnv == "Firefox")
            {
                // Specify Correct location of geckodriver.exe folder path. Ex: C:/Project/drivers

                driver = new FirefoxDriver(path + @"\drivers\");
            }
            else
            {
                throw new Exception("Unrecognized Browser Environment!");
            }
        }

        [Test, Order(1)]
        public void NavigateToSignIn()
        {
            driver.Navigate().GoToUrl(Configuration.WebPages.SignInPage);
            try
            {
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(3));
                var signInButton = wait.Until(driver => driver.FindElement(By.Id(Configuration.Pages[0][0].Id)));
            }
            catch (WebDriverTimeoutException)
            {
                Assert.Fail($"Sign In button was not found within the timeout period of {3} second(s).");
            }
            Assert.Pass();
        }

        [Test, Order(2)]
        public void ExecuteFlow()
        {

            for(int i = 0; i < Configuration.Pages.Length; i++)
            {
                var pageActions = Configuration.Pages[i];
                if (i > 0)
                {
                    try
                    {
                        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(5));
                        wait.Until(driver => driver.FindElement(By.Id(pageActions[0].Id)));
                    }
                    catch (WebDriverTimeoutException)
                    {
                        Assert.Fail($"Sign in was not completed within the timeout period of {5} second(s).");
                    }
                }
                for (int j = 0; j < pageActions.Length; j++)
                {
                    if(pageActions[j].InputType == "text")
                    {
                        driver.FindElement(By.Id(pageActions[j].Id)).SendKeys(pageActions[j].Value);
                    }
                    else if (pageActions[j].InputType == "button")
                    {
                        driver.ExecuteJavaScript($"document.getElementById('{pageActions[j].Id}').click()");
                    }
                    else if(pageActions[j].InputType == "dropdown")
                    {
                        driver.ExecuteJavaScript($"$('#{pageActions[j].Id}').val('{pageActions[j].Value}')");
                    }
                    else if (pageActions[j].InputType == "checkbox")
                    {
                        if(pageActions[j].Value == "true")
                            driver.ExecuteJavaScript($"$('#{pageActions[j].Id}').attr('checked', true)");
                        else
                            driver.ExecuteJavaScript($"$('#{pageActions[j].Id}').attr('checked', false)");
                    }
                    else if (pageActions[j].InputType.Contains("Fn::")){
                        var fnName = pageActions[j].InputType.Substring(4);
                        switch (fnName)
                        {
                            case "otpEmail":
                                GetEmailOTP();
                                break;
                        }
                    }
                }
            }

            bool successPage = false;

            try
            {
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(90));
                successPage = wait.Until(driver => driver.Url.Contains(Configuration.WebPages.SuccessPage));
            }
            catch (WebDriverTimeoutException)
            {
                Assert.Fail($"Sign in was not completed within the timeout period of {90} second(s).");
            }

            Assert.IsTrue(successPage);
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            driver.Quit();
        }
    }
}