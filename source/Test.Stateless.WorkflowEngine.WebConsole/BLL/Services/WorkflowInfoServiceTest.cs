﻿using NSubstitute;
using NUnit.Framework;
using Stateless.WorkflowEngine;
using Stateless.WorkflowEngine.Stores;
using Stateless.WorkflowEngine.WebConsole.BLL.Data.Models;
using Stateless.WorkflowEngine.WebConsole.BLL.Factories;
using Stateless.WorkflowEngine.WebConsole.BLL.Services;
using Stateless.WorkflowEngine.WebConsole.ViewModels.Connection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test.Stateless.WorkflowEngine.WebConsole.BLL.Services
{
    [TestFixture]
    public class WorkflowInfoServiceTest
    {
        private IWorkflowInfoService _workflowInfoService;
        private IWorkflowStoreFactory _workflowStoreFactory;

        [SetUp]
        public void WorkflowStoreInfoServiceTest_SetUp()
        {
            _workflowStoreFactory = Substitute.For<IWorkflowStoreFactory>();

            _workflowInfoService = new WorkflowInfoService(_workflowStoreFactory);
        }

        #region GetWorkflowStoreInfo Tests

        [Test]
        public void GetWorkflowStoreInfo_ModelIsNull_ThrowsException()
        {
            TestDelegate del = () => _workflowInfoService.GetWorkflowStoreInfo(null);
            // assert
            Assert.Throws<ArgumentNullException>(del);
        }

        [Test]
        public void GetWorkflowStoreInfo_ClientErrorOccurs_PopulatesModelWithException()
        {
            const string exceptionMessage = "test exception";
            ConnectionModel connectionModel = new ConnectionModel();

            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();
            workflowStore.When(x => x.GetIncompleteCount()).Do((ci) => { throw new Exception(exceptionMessage); });
            _workflowStoreFactory.GetWorkflowStore(connectionModel).Returns(workflowStore);

            ConnectionInfoViewModel model = _workflowInfoService.GetWorkflowStoreInfo(connectionModel);

            Assert.AreEqual(exceptionMessage, model.ConnectionError);
            Assert.IsNull(model.ActiveCount);
        }

        [Test]
        public void GetWorkflowStoreInfo_NoClientErrorOccurs_PopulatesModelWithCounts()
        {
            Random r = new Random();
            long activeCount = r.Next(11, 1000);
            long suspendedCount = r.Next(1, 10);
            long completedCount = r.Next(1001, 10000);

            ConnectionModel connectionModel = new ConnectionModel();

            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();
            workflowStore.GetIncompleteCount().Returns(activeCount);
            workflowStore.GetSuspendedCount().Returns(suspendedCount);
            workflowStore.GetCompletedCount().Returns(completedCount);
            _workflowStoreFactory.GetWorkflowStore(connectionModel).Returns(workflowStore);

            ConnectionInfoViewModel model = _workflowInfoService.GetWorkflowStoreInfo(connectionModel);

            Assert.AreEqual(activeCount, model.ActiveCount);
            Assert.AreEqual(completedCount, model.CompleteCount);
            Assert.AreEqual(suspendedCount, model.SuspendedCount);
        }

        #endregion
    }
}
