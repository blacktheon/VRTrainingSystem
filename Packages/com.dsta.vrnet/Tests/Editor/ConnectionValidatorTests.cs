using System.Text;
using NUnit.Framework;
using DSTA.VRNet.Core;

namespace DSTA.VRNet.Tests
{
    public class ConnectionValidatorTests
    {
        static byte[] Payload(string guid = "5f0c7c9e-9d3a-4a5e-8b21-2f6f0a34d111",
                              string team = "TEAM-A", string version = "0.1.0", string name = "Trainee 1")
        {
            return new ConnectionPayload
            {
                playerGuid = guid, teamId = team, appVersion = version, playerName = name
            }.ToBytes();
        }

        [Test]
        public void Validate_MatchingTeamAndVersion_Approves()
        {
            var result = ConnectionValidator.Validate(Payload(), "TEAM-A", "0.1.0");
            Assert.IsTrue(result.Approved);
            Assert.AreEqual("Trainee 1", result.Payload.playerName);
        }

        [Test]
        public void Validate_WrongTeam_Rejects()
        {
            var result = ConnectionValidator.Validate(Payload(team: "TEAM-B"), "TEAM-A", "0.1.0");
            Assert.IsFalse(result.Approved);
            StringAssert.Contains("team", result.Reason.ToLower());
        }

        [Test]
        public void Validate_WrongVersion_Rejects()
        {
            var result = ConnectionValidator.Validate(Payload(version: "9.9.9"), "TEAM-A", "0.1.0");
            Assert.IsFalse(result.Approved);
            StringAssert.Contains("version", result.Reason.ToLower());
        }

        [Test]
        public void Validate_MalformedPayload_Rejects()
        {
            var result = ConnectionValidator.Validate(Encoding.UTF8.GetBytes("garbage"), "TEAM-A", "0.1.0");
            Assert.IsFalse(result.Approved);
        }

        [Test]
        public void Validate_InvalidGuid_Rejects()
        {
            var result = ConnectionValidator.Validate(Payload(guid: "not-a-guid"), "TEAM-A", "0.1.0");
            Assert.IsFalse(result.Approved);
        }
    }
}
