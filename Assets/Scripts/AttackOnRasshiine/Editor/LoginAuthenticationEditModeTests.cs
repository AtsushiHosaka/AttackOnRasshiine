using System.Collections.Generic;
using AttackOnRasshiine.Runtime.Data;
using AttackOnRasshiine.Runtime.Scene;
using AttackOnRasshiine.Runtime.Services;
using NUnit.Framework;

namespace AttackOnRasshiine.Editor
{
    public sealed class LoginAuthenticationEditModeTests
    {
        [Test]
        public void LocalLoginAcceptsSeedMemberAndMentorCredentials()
        {
            var repository = new LocalGameRepository();
            var member = repository.Members[0];
            var mentor = repository.Mentors[0];

            var memberLogin = repository.Authenticate($" {member.LoginId.ToUpperInvariant()} ", "password");
            var mentorLogin = repository.Authenticate($" {mentor.LoginId.ToUpperInvariant()} ", "password");

            Assert.AreSame(member, memberLogin);
            Assert.AreEqual(UserRole.Member, memberLogin.Role);
            Assert.AreSame(mentor, mentorLogin);
            Assert.AreEqual(UserRole.Mentor, mentorLogin.Role);
        }

        [Test]
        public void LocalLoginRejectsInvalidCredentialsAndInactiveAccounts()
        {
            var repository = new LocalGameRepository();
            var member = repository.Members[0];

            Assert.IsNull(repository.Authenticate(member.LoginId, "wrong-password"));
            Assert.IsNull(repository.Authenticate("missing-user", "password"));

            member.IsActive = false;

            Assert.IsNull(repository.Authenticate(member.LoginId, "password"));
        }

        [Test]
        public void ApplyingAuthoritativeUsersDoesNotInventDefaultLocalPasswords()
        {
            var repository = new LocalGameRepository();
            repository.ApplySnapshot(new GameSnapshot
            {
                Users = new List<UserProfile>
                {
                    new()
                    {
                        Id = "7f86cde0-2f53-4e8a-a4f0-1ca95fd2a4d8",
                        LoginId = "remote-member",
                        Nickname = "Remote Member",
                        Role = UserRole.Member,
                        IsActive = true,
                        InitialPasswordChanged = true
                    }
                }
            });

            Assert.IsNull(repository.Authenticate("remote-member", "password"));
        }

        [Test]
        public void AuthenticatedHomeSceneMatchesUserRole()
        {
            Assert.AreEqual(RasshiineProductionScene.MemberHome, RasshiineSceneCatalog.GetAuthenticatedHomeScene(UserRole.Member));
            Assert.AreEqual(RasshiineProductionScene.MentorDashboard, RasshiineSceneCatalog.GetAuthenticatedHomeScene(UserRole.Mentor));
        }

        [Test]
        public void RuntimeSessionNeverRetainsMalformedBearerTokens()
        {
            try
            {
                RasshiineRuntimeSession.SetSessionToken(" valid-token ");
                Assert.AreEqual("valid-token", RasshiineRuntimeSession.SessionToken);

                RasshiineRuntimeSession.SetSessionToken("contains a space");
                Assert.AreEqual(string.Empty, RasshiineRuntimeSession.SessionToken);
                RasshiineRuntimeSession.SetSessionToken(new string('x', SupabaseSessionToken.MaximumLength + 1));
                Assert.AreEqual(string.Empty, RasshiineRuntimeSession.SessionToken);
            }
            finally
            {
                RasshiineRuntimeSession.Clear();
            }
        }
    }
}
