using System.Collections.Generic;
using System.Reflection;
using DSTA.VRNet.Core;
using NUnit.Framework;
using UnityEngine;

namespace DSTA.VRNet.Tests
{
    public class VRNetSessionTests
    {
        readonly List<Object> _objects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var value in _objects)
                if (value != null)
                {
                    if (value is GameObject go && go.TryGetComponent<VRNetSession>(out var session))
                        InvokeLifecycle(session, "OnDestroy");
                    Object.DestroyImmediate(value);
                }
            _objects.Clear();
            VRNetSession.SetLocalRig(null, null, null, null);
            // Keep failures against the old lifecycle from polluting later tests.
            typeof(VRNetSession).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, null);
        }

        [Test]
        public void Config_ChangesBeforeAwakeDoNotModifyTheAssignedAsset()
        {
            var source = CreateConfig();
            var session = CreateSession(source, initialize: false);

            session.Config.teamId = "RUNTIME-TEAM";
            session.Config.serverPort = 8123;

            Assert.That(source.teamId, Is.EqualTo("ASSET-TEAM"));
            Assert.That(source.serverPort, Is.EqualTo(7777));
            Assert.That(session.Config.teamId, Is.EqualTo("RUNTIME-TEAM"));
            Assert.That(session.Config.serverPort, Is.EqualTo(8123));
        }

        [Test]
        public void DestroySession_ReleasesRuntimeConfigAndSingleton()
        {
            var source = CreateConfig();
            var session = CreateSession(source);
            var runtime = session.Config;

            DestroySession(session);

            Assert.That(VRNetSession.Instance, Is.Null);
            Assert.That(runtime == null, Is.True, "The session owns and must destroy its runtime configuration.");
            Assert.That(source != null, Is.True, "Destroying a session must retain the assigned config asset.");
        }

        [Test]
        public void DestroySession_ClearsRegisteredRigEvenWhenItsTransformsSurvive()
        {
            var session = CreateSession(CreateConfig());
            var rig = CreateGameObject("Registered rig").transform;
            VRNetSession.SetLocalRig(rig, rig, rig, rig);

            DestroySession(session);

            Assert.That(IsLocalRigSet(), Is.False);
            Assert.That(rig != null, Is.True);
        }

        [Test]
        public void StopSession_WithoutNetworkManagerCanRepeatAndPreservesRigForRestart()
        {
            var session = CreateSession(CreateConfig());
            var rig = CreateGameObject("Registered rig").transform;
            VRNetSession.SetLocalRig(rig, rig, rig, rig);

            Assert.DoesNotThrow(() => session.StopSession());
            Assert.DoesNotThrow(() => session.StopSession());

            Assert.That(IsLocalRigSet(), Is.True);
        }

        [Test]
        public void StopSession_NotifiesAfterClearingRosterAndDisablingReconnect()
        {
            var session = CreateSession(CreateConfig());
            session.Registry.RecordApproved(7, new ConnectionPayload
                { playerGuid = "7bd27c68-f9d8-4ec8-9ba0-3fbe6299605a", playerName = "Trainee" });
            var reconnect = session.GetComponent<ReconnectController>();
            reconnect.Enabled = true;
            bool notified = false;
            session.OnStopped += () =>
            {
                notified = true;
                Assert.That(session.Registry.TryGetByClient(7, out _), Is.False);
                Assert.That(reconnect.Enabled, Is.False);
            };

            session.StopSession();

            Assert.That(notified, Is.True);
        }

        VRNetConfig CreateConfig()
        {
            var config = ScriptableObject.CreateInstance<VRNetConfig>();
            config.teamId = "ASSET-TEAM";
            _objects.Add(config);
            return config;
        }

        VRNetSession CreateSession(VRNetConfig config, bool initialize = true)
        {
            var go = CreateGameObject("Session under test");
            var session = go.AddComponent<VRNetSession>();
            typeof(VRNetSession).GetField("_config", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(session, config);
            if (initialize && VRNetSession.Instance != session)
                InvokeLifecycle(session, "Awake");
            return session;
        }

        static void DestroySession(VRNetSession session)
        {
            // EditMode objects have not entered the player lifecycle, so Unity does
            // not guarantee dispatch of Awake/OnDestroy. Exercise our callbacks directly.
            InvokeLifecycle(session, "OnDestroy");
            Object.DestroyImmediate(session.gameObject);
        }

        static void InvokeLifecycle(VRNetSession session, string method)
        {
            typeof(VRNetSession).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session, null);
        }

        GameObject CreateGameObject(string name)
        {
            var go = new GameObject(name);
            _objects.Add(go);
            return go;
        }

        static bool IsLocalRigSet()
        {
            var rigType = typeof(VRNetSession).Assembly.GetType("DSTA.VRNet.Avatar.LocalRig");
            return (bool)rigType.GetProperty("IsSet", BindingFlags.Public | BindingFlags.Static).GetValue(null);
        }
    }
}
