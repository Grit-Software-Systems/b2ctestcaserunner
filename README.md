# B2C Test Driver

## appsettings.json

This JSON contains the relevant data for your test:

{
  "WebPages": {
    "SignInPage": "The Run now endpoint of your B2C policy",
    "SuccessPage": "The reply URL of your application, in testing this is generally: https://jwt.ms"
  },
  "TestConfiguration": {
    "Environment": "The browser you want to run the test, currently: Chrome or Firefox",
    "TimeOut": "How long wait actions should hold before timing out"
  },
  "Pages": See Pages section
}

### Pages in appsettings.json

The Pages section in appsettings.json is a 2-D array containing the flow of your test. Each child array is representative of a new page navigation in B2C and the test will check to see if the first element at the start of the next page is present or the timeout duration elapses.

Each action on the page is represented by simple json object, in the following format:

{
    "id": string,
    "inputType": "text"|"dropdown"|"checkbox"|"button"|"Fn::{value in switch statement}",
    "value"?: string (only used by text, dropdown, or checkbox for now)
}

## Distributing a test

This can be built in Release mode and the test can be ran form the console with the following command (the machine must have dotnet installed):

dotnet test {1} -- NUnit.TestOutputXml={2}

1: relative or absolute path to B2CTestDriver.dll
2: folder to contain the XML results, relative to B2CTestDriver.dll. The folder will be created if it does not exist

example:

dotnet test .\Release\net6.0\B2CTestDriver.dll -- NUnit.TestOutputXml=..\..\results

The test that is ran can be altered just by changing appsettings.json