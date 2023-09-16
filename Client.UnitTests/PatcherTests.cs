using Archipelago.MonsterSanctuary.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace Client.UnitTests
{
    [TestClass]
    public class PatcherTests
    {
        [TestMethod]
        public void GetGoldQuantity_Parses()
        {
            var gold = Patcher.GetGoldQuantity("100 G");
            Assert.AreEqual(100, gold);
        }

        [TestMethod]
        public void GetItemQuantity_Parses_1x()
        {
            string itemname = "Apple";
            var quantity = Patcher.GetQuantityOfItem(ref itemname);
            Assert.AreEqual(1, quantity);
            Assert.AreEqual("Apple", itemname);
        }

        [TestMethod]
        public void GetItemQuantity_Parses_2x()
        {
            string itemname = "2x Apple";
            var quantity = Patcher.GetQuantityOfItem(ref itemname);
            Assert.AreEqual(2, quantity);
            Assert.AreEqual("Apple", itemname);
        }

        [TestMethod]
        public void GetItemQuantity_Parses_3x()
        {
            string itemname = "3x Apple";
            var quantity = Patcher.GetQuantityOfItem(ref itemname);
            Assert.AreEqual(3, quantity);
            Assert.AreEqual("Apple", itemname);
        }
    }
}
