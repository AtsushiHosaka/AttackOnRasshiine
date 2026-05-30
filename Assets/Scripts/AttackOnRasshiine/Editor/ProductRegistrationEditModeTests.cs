using System;
using System.Linq;
using AttackOnRasshiine.Runtime.Services;
using NUnit.Framework;

namespace AttackOnRasshiine.Editor
{
    public sealed class ProductRegistrationEditModeTests
    {
        [Test]
        public void MemberCanRegisterPublicProductUrl()
        {
            var repository = new LocalGameRepository();
            var member = repository.Members[0];

            var product = repository.RegisterProduct(member.Id, "Neon Raid HUD", " https://example.com/raid ", "戦闘HUDの試作です。");

            Assert.AreEqual(member.Id, product.UserId);
            Assert.AreEqual("Neon Raid HUD", product.Title);
            Assert.AreEqual("https://example.com/raid", product.Url);
            Assert.AreEqual("戦闘HUDの試作です。", product.Description);
            Assert.IsTrue(product.IsPublic);
            Assert.IsNotEmpty(product.Id);
            Assert.Greater(product.CreatedAtUtc, DateTime.UtcNow.AddMinutes(-1));
            CollectionAssert.Contains(repository.GetVisibleProducts().Select(item => item.Id), product.Id);
        }

        [Test]
        public void InvalidProductUrlIsRejectedWithoutAddingEntry()
        {
            var repository = new LocalGameRepository();
            var member = repository.Members[0];
            var beforeCount = repository.Products.Count;

            var exception = Assert.Throws<InvalidOperationException>(() =>
                repository.RegisterProduct(member.Id, "Bad Link", "ftp://example.com/build", "プロトタイプ"));

            Assert.AreEqual("httpまたはhttpsのURLを入力してください。", exception.Message);
            Assert.AreEqual(beforeCount, repository.Products.Count);
        }

        [Test]
        public void MentorCanHideInappropriateProductUrl()
        {
            var repository = new LocalGameRepository();
            var member = repository.Members[0];
            var mentor = repository.Mentors[0];
            var product = repository.RegisterProduct(member.Id, "Release Page", "https://example.com/release", "公開ページ");

            repository.HideProduct(product.Id, mentor.Id);

            Assert.IsFalse(product.IsPublic);
            Assert.AreEqual(mentor.Id, product.HiddenBy);
            CollectionAssert.DoesNotContain(repository.GetVisibleProducts().Select(item => item.Id), product.Id);
            CollectionAssert.Contains(repository.GetProductsForMentor().Select(item => item.Id), product.Id);
        }

        [Test]
        public void MemberCannotHideProductUrl()
        {
            var repository = new LocalGameRepository();
            var owner = repository.Members[0];
            var otherMember = repository.Members[1];
            var product = repository.RegisterProduct(owner.Id, "Playable Demo", "https://example.com/demo", "操作確認用");

            Assert.Throws<InvalidOperationException>(() => repository.HideProduct(product.Id, otherMember.Id));

            Assert.IsTrue(product.IsPublic);
            Assert.IsEmpty(product.HiddenBy);
        }
    }
}
