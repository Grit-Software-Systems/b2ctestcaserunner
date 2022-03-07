# B2C Test Driver

B2C test driver is a utility for B2C developers to easily script and run a Selenium test against their site.

## Test Configuration

This JSON contains the relevant data for your test:

```json
{
  "TestConfiguration": {
    "Environment": "The browser you want to run the test, currently: Chrome or Firefox",
    "OTP_Age": "Grit specific field - the max age of emails for the OTP API - in seconds",
    "TimeOut": "How long wait actions should hold before timing out - in seconds"
  },
  "Tests": ["An array of test names to be ran in this suite"]
}
```

### Test json

A test json is an object with each key denoting a step in the test and values being an array of actions to perform on a page. Between pages the test will check to see if the first element at the start of the next page is present or the timeout duration elapses.

Each action on the page is represented by simple json object, in the following format:

```typescript
interface Action {
    "id": string,
    "inputType": "testCaseStart"|"testCaseComplete"|"text"|"dropdown"|"checkbox"|"button"|"Fn::{value in switch statement}",
    "value"?: string
}
```

Every test must have a testCaseStart Action as the first Action on the first page and a tesCaseComplete Action as the final action on the last page in order for it to be a valid test.

## Distributing a test

This can be built in Release mode and the test can be ran form the console with the following command (the machine must have dotnet installed):

```
dotnet test {1} -- NUnit.WorkDirectory={2} NUnit.TestOutputXml={3}
```

1. Relative or absolute path to B2CTestDriver.dll
2. The directory (relative or absolute) or Azure blob storage that contains the test configuration json
3. Folder to contain the XML results, either relative to B2CTestDriver.dll or an absolute path. The folder will be created if it does not exist

example:

```
dotnet test .\Release\net6.0\B2CTestDriver.dll -- NUnit.WorkDirectory=..\..\Tests NUnit.TestOutputXml=..\..\results
```

The tests that are ran can be altered just by changing the test configuration json