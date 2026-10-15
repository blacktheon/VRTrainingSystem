using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class DemoSessionStateTests
{
    // A UI click before networking starts must never create local-only night state.
    [Test]
    public void ToggleBeforeServerSpawn_IsRejectedAndLeavesDayState()
    {
        var type = Type.GetType("DemoSessionState, Assembly-CSharp");
        Assert.That(type, Is.Not.Null, "The demo needs a networked session state component.");
        var go = new GameObject("Unspawned session state");
        try
        {
            var state = go.AddComponent(type);
            Assert.That(type.GetMethod("ToggleDayNight").Invoke(state, null), Is.EqualTo(false));
            Assert.That(type.GetProperty("IsNight").GetValue(state), Is.EqualTo(false));
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }
}
