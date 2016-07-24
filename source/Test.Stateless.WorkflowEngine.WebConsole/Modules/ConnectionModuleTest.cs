﻿using AutoMapper;
using Encryption;
using Nancy;
using Nancy.Authentication.Forms;
using Nancy.Bootstrapper;
using Nancy.Responses.Negotiation;
using Nancy.Testing;
using Newtonsoft.Json;
using NSubstitute;
using NUnit.Framework;
using Stateless.WorkflowEngine.WebConsole.BLL.Data.Models;
using Stateless.WorkflowEngine.WebConsole.BLL.Data.Stores;
using Stateless.WorkflowEngine.WebConsole.BLL.Security;
using Stateless.WorkflowEngine.WebConsole.BLL.Services;
using Stateless.WorkflowEngine.WebConsole.BLL.Validators;
using Stateless.WorkflowEngine.WebConsole.Modules;
using Stateless.WorkflowEngine.WebConsole.Navigation;
using Stateless.WorkflowEngine.WebConsole.ViewModels;
using Stateless.WorkflowEngine.WebConsole.ViewModels.Login;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test.Stateless.WorkflowEngine.WebConsole.Modules
{
    [TestFixture]
    public class ConnectionModuleTest
    {
        private ConnectionModule _connectionModule;
        private IUserStore _userStore;
        private IConnectionValidator _connectionValidator;
        private IEncryptionProvider _encryptionProvider;
        private IWorkflowStoreInfoService _workflowStoreService;

        [SetUp]
        public void UserModuleTest_SetUp()
        {
            _userStore = Substitute.For<IUserStore>();
            _encryptionProvider = Substitute.For<IEncryptionProvider>();
            _connectionValidator = Substitute.For<IConnectionValidator>();
            _workflowStoreService = Substitute.For<IWorkflowStoreInfoService>();

            _connectionModule = new ConnectionModule(_userStore, _connectionValidator, _encryptionProvider, _workflowStoreService);

            Mapper.Initialize((cfg) =>
            {
                cfg.CreateMap<ConnectionModel, WorkflowStoreModel>();
            });

        }

        #region List Tests

        [Test]
        public void List_OnExecute_LoadsAllConnectionsForCurrentUser()
        {
            // setup
            var bootstrapper = this.ConfigureBootstrapperAndUser();
            var browser = new Browser(bootstrapper);
            _connectionValidator.Validate(Arg.Any<ConnectionModel>()).Returns(new ValidationResult("error"));

            int connectionCount = new Random().Next(3, 9);
            UserModel user = new UserModel()
            {
                Id = bootstrapper.CurrentUser.Id,
                UserName = bootstrapper.CurrentUser.UserName,
            };
            for (var i = 0; i < connectionCount; i++)
            {
                ConnectionModel conn = new ConnectionModel();
                user.Connections.Add(conn);
            }
            _userStore.GetUser(user.UserName).Returns(user);

            // execute
            var response = browser.Get(Actions.Connection.List, (with) =>
            {
                with.HttpRequest();
                with.FormsAuth(bootstrapper.CurrentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
            });

            // assert
            _workflowStoreService.Received(connectionCount).PopulateWorkflowStoreInfo(Arg.Any<WorkflowStoreModel>());

        }

        #endregion

        #region Save Tests

        [Test]
        public void Save_InvalidModel_ReturnsError()
        {
            // setup
            var bootstrapper = this.ConfigureBootstrapperAndUser();
            var browser = new Browser(bootstrapper);
            _connectionValidator.Validate(Arg.Any<ConnectionModel>()).Returns(new ValidationResult("error"));

            // execute
            var response = browser.Post(Actions.Connection.Save, (with) =>
            {
                with.HttpRequest();
                with.FormsAuth(bootstrapper.CurrentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
            });

            // assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

            ValidationResult result = JsonConvert.DeserializeObject<ValidationResult>(response.Body.AsString());
            Assert.IsFalse(result.Success);
            Assert.AreEqual(1, result.Messages.Count);
            _encryptionProvider.DidNotReceive().SimpleEncrypt(Arg.Any<string>(), Arg.Any<byte[]>(), null);
            _userStore.DidNotReceive().Save();
        }

        [Test]
        public void Save_NoPassword_DoesNotEncrypt()
        {
            // setup
            var bootstrapper = this.ConfigureBootstrapperAndUser();
            var browser = new Browser(bootstrapper);
            _connectionValidator.Validate(Arg.Any<ConnectionModel>()).Returns(new ValidationResult());

            // execute
            var response = browser.Post(Actions.Connection.Save, (with) =>
            {
                with.HttpRequest();
                with.FormValue("Password", "");
                with.FormsAuth(bootstrapper.CurrentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
            });

            // assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

            ValidationResult result = JsonConvert.DeserializeObject<ValidationResult>(response.Body.AsString());
            Assert.IsTrue(result.Success);
            Assert.AreEqual(0, result.Messages.Count);
            _encryptionProvider.DidNotReceive().SimpleEncrypt(Arg.Any<string>(), Arg.Any<byte[]>(), null);
            _userStore.Received(1).Save();
        }

        [Test]
        public void Save_WithPassword_DoesEncryptAndSaves()
        {
            // setup
            byte[] key = new byte[20];
            new Random().NextBytes(key);
            string password = "testPassword";
            string encryptedPassword = Guid.NewGuid().ToString();

            var bootstrapper = this.ConfigureBootstrapperAndUser();
            var browser = new Browser(bootstrapper);
            _connectionValidator.Validate(Arg.Any<ConnectionModel>()).Returns(new ValidationResult());
            _encryptionProvider.NewKey().Returns(key);
            _encryptionProvider.SimpleEncrypt(password, key, null).Returns(encryptedPassword);

            // execute
            var response = browser.Post(Actions.Connection.Save, (with) =>
            {
                with.HttpRequest();
                with.FormValue("Password", password);
                with.FormsAuth(bootstrapper.CurrentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
            });

            // assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

            ValidationResult result = JsonConvert.DeserializeObject<ValidationResult>(response.Body.AsString());
            Assert.IsTrue(result.Success);
            Assert.AreEqual(0, result.Messages.Count);
            _encryptionProvider.Received(1).SimpleEncrypt(password, key, null);

            Assert.AreEqual(encryptedPassword, _userStore.Users[0].Connections[0].Password);
            _userStore.Received(1).Save();
        }


        #endregion

        #region Private Methods

        private ModuleTestBootstrapper ConfigureBootstrapperAndUser()
        {
            var bootstrapper = new ModuleTestBootstrapper();
            bootstrapper.Login();
            bootstrapper.ConfigureRequestContainerCallback = (container) =>
            {
                container.Register<IUserStore>(_userStore);
                container.Register<IEncryptionProvider>(_encryptionProvider);
                container.Register<IConnectionValidator>(_connectionValidator);
                container.Register<IWorkflowStoreInfoService>(_workflowStoreService);
            };

            // set up the logged in user
            UserModel user = new UserModel()
            {
                Id = bootstrapper.CurrentUser.Id,
                UserName = bootstrapper.CurrentUser.UserName,
                Role = bootstrapper.CurrentUser.Claims.First()
            };
            List<UserModel> users = new List<UserModel>() { user };
            _userStore.Users.Returns(users);

            return bootstrapper;
        }

        #endregion


    }
}