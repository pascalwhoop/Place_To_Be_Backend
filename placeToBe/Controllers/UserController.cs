﻿using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using Microsoft.Owin.Security.Facebook;
using placeToBe.Model.Entities;
using placeToBe.Model.Repositories;
using placeToBe.Services;

namespace placeToBe.Controllers
{
    public class UserController : ApiController
    {
        private readonly AccountService accountService = new AccountService();

        /// <summary>
        ///     PUT- Send an activationemail and register a accountService with email and passwort
        /// </summary>
        /// <param name="userEmail"></param>
        /// <param name="userPassword"></param>
        /// <returns></returns>
        [System.Web.Http.Route("api/user/")]
        public async Task<User> Post(User user)
        {
            return await accountService.createUser(user);
        }

        /// <summary>
        ///     GET- accountService by activationcode for confirm mail.
        /// </summary>
        /// <param name="activationcode"></param>
        /// <returns></returns>
        [System.Web.Http.Route("api/user/")]
        public async Task<bool> Get([FromUri] string activationcode)
        {
            return await accountService.ConfirmEmail(activationcode);
        }

        /// <summary>
        ///     PUT- Login - Get AuthenticationTicket for 5 minutes
        /// </summary>
        /// <param name="resetPasswordByMail"></param>
        /// <param name="userPassword"></param>
        /// <returns></returns>
        /*[PlaceToBeAuthenticationFilter]
        public async Task<Cookie> Put([FromUri] string userEmail) {
            return await accountService.Login(userEmail);
        }*/

        /// <summary>
        ///     PUT- Reset the old password and send a new one to the email.
        /// </summary>
        /// <param name="userEmail"></param>
        /// <returns></returns>
        [System.Web.Http.Route("api/user/{userEmail}/password_reset")]
        public async Task Post([FromUri] string userEmail)
        {
            await accountService.ForgetPasswordReset(userEmail);
        }

        /// <summary>
        ///     PUT- Change the password from accountService
        /// </summary>
        /// <param name="userEmail"></param>
        /// <param name="oldPassword"></param>
        /// <param name="newPassword"></param>
        /// <returns></returns>
        [PlaceToBeAuthenticationFilter]
        [System.Web.Http.Route("api/user/{userEmail}/password_change")]
        public async Task<JsonResponse> Put(PasswordChangePair pcp)
        {

            var code = await accountService.ChangePassword(pcp.email, pcp.oldPassword, pcp.newPassword);
            HttpContext.Current.Response.StatusCode = (int)code;
            switch (code)
            {
                case HttpStatusCode.OK:
                    return new JsonResponse
                    {
                        status = "OK",
                        message = "Password changed successfully.",
                        showUser = true
                    };
                case HttpStatusCode.BadRequest:
                    return new JsonResponse
                    {
                        status = "Error",
                        message = "False password.",
                        showUser = true
                    };
                case HttpStatusCode.Conflict:
                    return new JsonResponse
                    {
                        status = "Error",
                        message = "Database timeout.",
                        showUser = false
                    };
                case HttpStatusCode.NotFound:
                    return new JsonResponse
                    {
                        status = "Error",
                        message = "User not found.",
                        showUser = false
                    };
            }
            return new JsonResponse
            {
                status = "Error",
                message = "Error occured.",
                showUser = false
            };


        }

        [PlaceToBeAuthenticationFilter]
        [System.Web.Http.Route("api/user/authorize")]
        public async Task<ActionResult> Get()
        {
            return new HttpStatusCodeResult(HttpStatusCode.Accepted);
        }
    }
}