using FakeItEasy;
using IngameScript;
using NUnit.Framework;
using MotherCore.TestUtilities;
using Sandbox.ModAPI.Ingame;
using System.Net.NetworkInformation;
using System;
using System.Collections.Generic;

//using System.Collections.Generic;

namespace MotherCore.Tests.Tests
{
    public class SecurityTests : BaseModuleTests
    {
        [Test]
        public void It_Can_Be_Instantiated_With_An_Instance_of_Mother()
        {
            Security security = new Security();

            //Assert.That(security.Mother, Is.SameAs(_mother));
        }

        [Test]
        public void A_String_Can_Be_Encrypted()
        {
            Security security = new Security();
            string originalString = "Hello, World!";

            string encryptedString = Security.Encrypt(originalString, "secret");

            Assert.That(encryptedString, Is.Not.EqualTo(originalString));
            Assert.That(Security.IsEncrypted(originalString), Is.False);
            Assert.That(Security.IsEncrypted(encryptedString), Is.True);
        }

        [Test]
        public void A_String_Can_Be_Decrypted()
        {
            Security security = new Security();
            string originalString = "Hello, World!";
            string passcodeCorrect = "correct_passcode";
            string passcodeIncorrect = "wrong_passcode";

            string encryptedString = Security.Encrypt(originalString, passcodeCorrect);

            Assert.That(Security.IsEncrypted(encryptedString), Is.True);

            string decryptedStringCorrect = Security.Decrypt(encryptedString, passcodeCorrect);

            Assert.That(decryptedStringCorrect, Is.EqualTo(originalString));

            string decryptedStringIncorrect = Security.Decrypt(encryptedString, passcodeIncorrect);

            Assert.That(decryptedStringIncorrect, Is.Not.EqualTo(originalString));
        }
    }
}
