using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

// Persists results across the integration test's EnterPlayMode domain reload.
[InitializeOnLoad]
public static class DemoTestRunReporter
{
    static DemoTestRunReporter() => TestRunnerApi.RegisterTestCallback(new Reporter());

    sealed class Reporter : ICallbacks
    {
        public void RunStarted(ITestAdaptor test) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            Directory.CreateDirectory("Logs");
            TestRunnerApi.SaveResultToFile(result, "Logs/vrnet-all-results.xml");
            Debug.Log($"[VRNET TESTS] {result.ResultState}: {result.PassCount} passed, {result.FailCount} failed");
        }
    }
}
