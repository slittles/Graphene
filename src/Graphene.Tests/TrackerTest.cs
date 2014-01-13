﻿// Copyright 2013-2014 Boban Jose
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Graphene.Data;
using Graphene;
using Graphene.Tracking;

namespace Graphene.Tests
{
    public class FakePersister : Publishing.IPersist
    {
        private List<TrackerData> _trackingDate = new List<Data.TrackerData>();
        private object _lock = new object();
        public async void Persist(TrackerData trackerData)
        {
            await Task.Run(() =>
            {
                lock (_lock)
                {
                    var td = _trackingDate.FirstOrDefault(td1 => td1.TimeSlot == trackerData.TimeSlot);

                    if (td != null)
                    {
                        td.Measurement.Max = Math.Max(td.Measurement.Max, trackerData.Measurement.Max);
                        td.Measurement.Min = Math.Max(td.Measurement.Min, trackerData.Measurement.Min);
                        td.Measurement.Total += trackerData.Measurement.Total;
                        td.Measurement.Occurrence += trackerData.Measurement.Occurrence;
                    }
                    else
                        _trackingDate.Add(trackerData);
                }
            });
        }
    }

    public class CustomerVisitTracker : ITrackable
    {
        public string Name { get { return "Customer Visit Tracker"; } }

        public string Description { get { return "Counts the number of customer visits"; } }

        public Resolution MinResolution { get { return Resolution.Hour; } }
    }

    public class CustomerPurchaseTracker : ITrackable
    {
        public string Name { get { return "Customer Purchase Tracker"; } }

        public string Description { get { return "Counts the number of customer purchases"; } }

        public Resolution MinResolution { get { return Resolution.Hour; } }
    }

    public struct CustomerFilter
    {
        public string State { get; set; }
        public string StoreID { get; set; }
        public string Gender { get; set; }
        public string Environment_ServerName { get; set; }
    }

    [TestClass]
    public class TrackerTest
    {
        private static int _task1Count = 0;
        private static int _task2Count = 0;
        private static int _task3Count = 0;

        [TestMethod]
        public void TestIncrement()
        {
            var ct = new System.Threading.CancellationTokenSource();


            Graphene.Configurator.Initialize(
                    new Configuration.Settings() { Persister = new Publishing.PersistToMongo("mongodb://localhost/Graphene") }
                );
            var task1 = Task.Run(() =>
            {
                while (!ct.IsCancellationRequested)
                {
                    //Graphene.Tracking.Container<PatientLinkValidationTracker>.IncrementBy(1);
                    Graphene.Tracking.Container<CustomerVisitTracker>
                        .Where<CustomerFilter>(
                            new CustomerFilter
                            {
                                State = "CA",
                                StoreID = "3234",
                                Environment_ServerName = "Server1"
                            }).IncrementBy(1);
                    _task1Count++;
                    System.Threading.Thread.Sleep(500);
                }
            }, ct.Token);

            var task2 = Task.Run(() =>
            {
                while (!ct.IsCancellationRequested)
                {
                    Graphene.Tracking.Container<CustomerPurchaseTracker>
                        .Where<CustomerFilter>(
                            new CustomerFilter
                            {
                                State = "MN",
                                StoreID = "334",
                                Environment_ServerName = "Server2"
                            }).IncrementBy(1);
                    _task2Count++;
                    System.Threading.Thread.Sleep(100);
                }
            }, ct.Token);

            var task3 = Task.Run(() =>
            {
                while (!ct.IsCancellationRequested)
                {
                    Tracking.Container<CustomerVisitTracker>.IncrementBy(3);
                    _task3Count++;
                    System.Threading.Thread.Sleep(500);
                }
            }, ct.Token);

            System.Threading.Thread.Sleep(1000);

            Graphene.Configurator.ShutDown();

            ct.Cancel();

            Task.WaitAll(task1, task2, task3);
        }
    }
}