using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PowerPosition.Core;
using TradingPlatform;

namespace PowerPosition.Tests
{
    [TestClass]
    public class ExtractorTests
    {
        /// <summary>
        ///     Test for volume aggregation method
        /// </summary>
        [TestMethod]
        public void AggregationTest()
        {
            var d = new DateTime(1, 1, 1);
            var trades = new List<Trade>();
            var trade1 = Trade.Create(d, 3);
            trade1.Periods[0].Volume = 10.110001010102;
            trade1.Periods[1].Volume = 15;
            trade1.Periods[2].Volume = 10000000001;
            trades.Add(trade1);
            var trade2 = Trade.Create(d, 3);
            trade2.Periods[0].Volume = 3;
            trade2.Periods[1].Volume = 10.1100010101071;
            trade2.Periods[2].Volume = 10.110001010102;
            trades.Add(trade2);
            
            var extractor = new Extractor();
            SortedDictionary<int, double> aggr = null;
            try
            {
               aggr = extractor.AggregateTrades(trades);
            }
            catch (Exception ex)
            {
                Assert.Fail("AggregateTrades exception:" + ex.Message);
            }

            var delta = 0.000000000001; //trades come with 11 decimals
            Assert.IsNotNull(aggr, "Aggregations are empty");
            Assert.AreEqual(3, aggr.Count, "Aggregations size is invalid");
            Assert.AreEqual(13.110001010102, aggr[1], delta, "Aggregation volume is incorrect, period 1");
            Assert.AreEqual (25.1100010101071, aggr[2], delta, "Aggregation volume is incorrect, period 2");
            Assert.AreEqual(10000000011.110001010102, aggr[3], delta, "Aggregation volume is incorrect, period 3");
            extractor.Stop();
        }

        /// <summary>
        ///     Test for output formatting method
        /// </summary>
        [TestMethod]
        public void TestOutputFormat()
        {
            var aggregations = new SortedDictionary<int, double>
            {
                [1] = 1,
                [2] = 22,
                [3] = 33.333,
                [4] = 44444444,
                [5] = 55555.0000500005,
                [6] = 606,
                [7] = 0.00000007,
                [8] = 8,
                [9] = 9,
                [10] = 1010000000000000,
                [11] = 11,
                [12] = 12,
                [13] = 13,
                [14] = 14,
                [15] = 15,
                [16] = 16,
                [17] = 17.7,
                [18] = 18,
                [19] = 19,
                [20] = 20,
                [21] = 21,
                [22] = 22,
                [23] = 23,
                [24] = 24
            };

            var extractor = new Extractor();
            IList<string> lines = null;
            try
            {
                lines = extractor.PrepareOutput(aggregations);
            }
            catch (Exception ex)
            {
                Assert.Fail("PrepareOutput exception:" + ex.Message);
            }
            Assert.IsNotNull(lines, "Output is empty");
            Assert.AreEqual(24, lines.Count, "Output size is invalid");

            foreach(var line in lines)
                Assert.IsFalse(string.IsNullOrEmpty(line), "Csv line is empty");

            Assert.AreEqual("23:00;1", lines[0], "Csv line 0 is incorrect");
            Assert.AreEqual("00:00;22", lines[1], "Csv line 1 is incorrect");
            Assert.AreEqual("01:00;33.333", lines[2], "Csv line 2 is incorrect");
            Assert.AreEqual("02:00;44444444", lines[3], "Csv line 3 is incorrect");
            Assert.AreEqual("03:00;55555.0000500005", lines[4], "Csv line 4 is incorrect");
            Assert.AreEqual("04:00;606", lines[5], "Csv line 5 is incorrect");
            Assert.AreEqual("05:00;7E-08", lines[6], "Csv line 6 is incorrect");
            Assert.AreEqual("06:00;8", lines[7], "Csv line 7 is incorrect");
            Assert.AreEqual("07:00;9", lines[8], "Csv line 8 is incorrect");
            Assert.AreEqual("08:00;1.01E+15", lines[9], "Csv line 9 is incorrect");
            Assert.AreEqual("09:00;11", lines[10], "Csv line 10 is incorrect");
            Assert.AreEqual("10:00;12", lines[11], "Csv line 11 is incorrect");
            Assert.AreEqual("11:00;13", lines[12], "Csv line 12 is incorrect");
            Assert.AreEqual("12:00;14", lines[13], "Csv line 13 is incorrect");
            Assert.AreEqual("13:00;15", lines[14], "Csv line 14 is incorrect");
            Assert.AreEqual("14:00;16", lines[15], "Csv line 15 is incorrect");
            Assert.AreEqual("15:00;17.7", lines[16], "Csv line 16 is incorrect");
            Assert.AreEqual("16:00;18", lines[17], "Csv line 17 is incorrect");
            Assert.AreEqual("17:00;19", lines[18], "Csv line 18 is incorrect");
            Assert.AreEqual("18:00;20", lines[19], "Csv line 19 is incorrect");
            Assert.AreEqual("19:00;21", lines[20], "Csv line 20 is incorrect");
            Assert.AreEqual("20:00;22", lines[21], "Csv line 21 is incorrect");
            Assert.AreEqual("21:00;23", lines[22], "Csv line 22 is incorrect");
            Assert.AreEqual("22:00;24", lines[23], "Csv line 23 is incorrect");
            
            extractor.Stop();
        }

        /// <summary>
        ///     Ensure all scheduled extractions run
        /// </summary>
        [TestMethod]
        public void TestTradingService()
        {
            //mock of csv writer that counts output dump attempts
            var csvMoq = new Mock<ICsvWriter>();
            var dumpCount = 0;
            csvMoq.Setup(m => m.Dump(It.IsAny<string>(), It.IsAny<IEnumerable<string>>())).Callback(() => { 
                dumpCount++; 
                //Trace.WriteLine("Dump"); 
            });

            //start extractor with 50 ms repeat extraction delay
            const int extractionsCount = 50;
            var extractor = new Extractor(csvMoq.Object, 50); //will 50 attempts ensure at least one exception? 
            var tasks = new List<Task>();
            for (var i = 0; i < extractionsCount; i++)
            {
                tasks.Add(extractor.Extract());
            }

            Task.WaitAll(tasks.ToArray());
            var currDumpCount = dumpCount;
            while (extractionsCount != dumpCount)
            {
                Thread.Sleep(100);
                if (currDumpCount == dumpCount)
                    break; //if no dumps happend in 100 ms, tis over

                currDumpCount = dumpCount;
            }
            Assert.AreEqual(extractionsCount, dumpCount);
        }
    }
}
