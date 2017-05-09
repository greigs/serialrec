using Microsoft.VisualStudio.TestTools.UnitTesting;
using SerialSender;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SerialSender.Tests
{
    [TestClass()]
    public class ProgramTests
    {
        [TestMethod()]
        public void CalculateRequiredMatchFullString()
        {
            Assert.IsNull(Program.CalculateRequiredMatch("%READY%", "sjhdfjhasgdfhjas%READY%"));
        }

        [TestMethod]
        public void CalculateRequiredMatchIfPartialMatch()
        {
            var result = Program.CalculateRequiredMatch("%READY%", "sdhgfjg%REA");
            Assert.AreEqual("DY%", result);
        }

        [TestMethod]
        public void CalculateRequiredMatchIfPartialMatchAtEndWithDuplicateChars()
        {
            var result = Program.CalculateRequiredMatch("%RE%ADY%", "sdfgdsfgsfsdhgf%RE%");
            Assert.AreEqual("ADY%", result);

            var result2 = Program.CalculateRequiredMatch("%REDO123%ADY456%", "sdfgdsfgsfsdfsdhgf%REDO123%");
            Assert.AreEqual("ADY456%", result2);

        }

        [TestMethod]
        public void CalculateRequiredMatchIfPartialMatch2()
        {
            var result = Program.CalculateRequiredMatch("%READY%", "sdfgdsfgsfsdhg%R");
            Assert.AreEqual("EADY%", result);

            var result2 = Program.CalculateRequiredMatch("%READY%", "sdfgdsfgsfsdhg%RE");
            Assert.AreEqual("ADY%", result2);

        }


    }
}