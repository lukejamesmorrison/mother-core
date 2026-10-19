using IngameScript;
using MotherCore.Tests.Utilities;
using NUnit.Framework;
using System;

namespace MotherCore.Tests.Tests.Unit
{
    [Category(TestCategories.LayerUnit)]
    public class SecurityTests
    {
        [Test]
        public void A_String_Can_Be_Encrypted()
        {
            string originalString = "Hello, World!";

            string encryptedString = Security.Encrypt(originalString, "secret");

            Assert.That(encryptedString, Is.Not.EqualTo(originalString));
            Assert.That(Security.IsEncrypted(originalString), Is.False);
            Assert.That(Security.IsEncrypted(encryptedString), Is.True);
        }

        [Test]
        public void A_String_Can_Be_Decrypted()
        {
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

        [Test]
        public void An_Empty_String_Can_Be_Encrypted_And_Decrypted()
        {
            string encryptedString = Security.Encrypt(string.Empty, "secret");

            Assert.That(encryptedString, Is.EqualTo("##"));
            Assert.That(Security.IsEncrypted(encryptedString), Is.True);
            Assert.That(Security.Decrypt(encryptedString, "secret"), Is.EqualTo(string.Empty));
        }

        [Test]
        public void Encryption_With_An_Empty_Passcode_Returns_The_Original_String()
        {
            string originalString = "Hello, World!";

            string encryptedString = Security.Encrypt(originalString, string.Empty);

            Assert.That(encryptedString, Is.EqualTo(originalString));
            Assert.That(Security.IsEncrypted(encryptedString), Is.False);
        }

        [Test]
        public void Decrypting_With_An_Empty_Passcode_Throws_For_Non_Empty_Encrypted_Payloads()
        {
            string encryptedString = Security.Encrypt("Hello, World!", "secret");

            Assert.That(
                () => Security.Decrypt(encryptedString, string.Empty),
                Throws.TypeOf<DivideByZeroException>());
        }

        [Test]
        public void Unencrypted_Inputs_Are_Not_Flagged_As_Encrypted()
        {
            Assert.That(Security.IsEncrypted(string.Empty), Is.False);
            Assert.That(Security.IsEncrypted("Hello, World!"), Is.False);
            Assert.That(Security.IsEncrypted("#not-encrypted"), Is.False);
            Assert.That(Security.IsEncrypted("##encrypted"), Is.True);
        }

        [Test]
        public void Decrypting_Unencrypted_Input_Does_Not_Return_The_Original_String()
        {
            string originalString = "Hello, World!";

            string decryptedString = Security.Decrypt(originalString, "secret");

            Assert.That(decryptedString, Is.Not.EqualTo(originalString));
        }
    }
}



