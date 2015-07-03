﻿using Newtonsoft.Json;
using placeToBe.Model;
using placeToBe.Model.Entities;
using placeToBe.Model.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;

namespace placeToBe.Services
{
    public class AccountService
    {
        private readonly String fbAppSecret = "469300d9c3ed9fe6ff4144d025bc1148";
        private readonly String fbAppId = "857640940981214";
        UserRepository userRepo = new UserRepository();
        FbUserRepository fbUserRepo = new FbUserRepository();
        int saltSize = 20;
        private string fromAddress = System.Configuration.ConfigurationManager.AppSettings["placeToBeEmail"];
        private string mailPassword = System.Configuration.ConfigurationManager.AppSettings["placeToBePasswordFromMail"];


        /// <summary>
        /// Checks if the users request is authorized by proofing users access token:
        /// 1. Check if it equals the users access token in the Database.
        /// 2. If not... inspecting access tokens (user and app access token) via Facebooks Graph API
        /// </summary>
        /// <param name="userAccessToken">The users access token which has to be validated</param>
        /// <param name="userPassword">The users Facebook-Id</param>
        /// <returns>true if access token is validated, otherwise false</returns>
        public async Task<bool> authorizeRequest(String userAccessToken, String userFbId)
        {
            
            FbUser user = await fbUserRepo.GetByFbIdAsync(userFbId);

            if (userAccessToken == user.shortAccessToken)
                return true;
            else
            {
                String appAccessToken = UtilService.performGetRequest(new Uri("https://graph.facebook.com/oauth/access_token?client_id=" + fbAppId + "&client_secret=" +
                              fbAppSecret + "&grant_type=client_credentials"));

                String jsonResponse = UtilService.performGetRequest(new Uri("https://graph.facebook.com/v2.3/debug_token?input_token=" + userAccessToken + "&access_token=" + appAccessToken));

                Inspection insp = JsonConvert.DeserializeObject<Inspection>(jsonResponse);

                if (insp.is_valid == true)
                {
                    user.shortAccessToken = userAccessToken;
                    await fbUserRepo.UpdateAsync(user);
                    return true;
                }
                else
                    return false;             
            }
        }

        /// <summary>
        /// SaveFBData to out database.
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public async Task SaveFBData(FbUser user)
        {
            FbUser fbuser = new FbUser();
            await fbUserRepo.InsertAsync(fbuser);
        }

        /// <summary>
        /// Login the user with given email and correct password.
        /// </summary>
        /// <param name="usersEmail"></param>
        /// <param name="userPassword"></param>
        /// <returns></returns>
        public async Task<FormsAuthenticationTicket> Login(string usersEmail, string userPassword)
        {
            try
            {
                byte[] userPasswordInBytes = Encoding.UTF8.GetBytes(userPassword);
                User user = await GetUserByEmail(usersEmail);
                byte[] salt = user.salt;
                byte[] passwordSalt = GenerateSaltedHash(userPasswordInBytes, salt);
                bool comparePasswords = CompareByteArrays(passwordSalt, user.passwordSalt);

                //statement: if users password is correct and status is activated          
                if (comparePasswords == true && user.status == true)
                {
                    //Set a ticket for five minutes to stay logged in.
                    FormsAuthenticationTicket ticket = new FormsAuthenticationTicket(usersEmail, false, 5);

                    if (ticket.Expired)
                    {
                        FormsAuthentication.SignOut();
                        return ticket;
                    }
                    return ticket;
                }
                else if (comparePasswords == true && user.status == false)
                {
                    UnauthorizedAccessException e;
                    //ToDo: UI-Message: e.Message..(user not found, password false, status not activated)..
                    return null;
                }
                else
                {
                    //ToDo: UI-Message: passwort is incorrect or not found
                    return null;
                }
            }
            catch (CookieException)
            {
                //ToDo: UI-Fehlermeldung : "Cant create Cookie"
                return null;
            }
        }

        //Log out the user and redirect to login-page.
        public void Logout()
        {
            FormsAuthentication.SignOut();
            FormsAuthentication.RedirectToLoginPage();
        }

        /// <summary>
        /// Changes the password from user (with given email) from old password to new password.
        /// </summary>
        /// <param name="userEmail"></param>
        /// <param name="oldPassword"></param>
        /// <param name="newPassword"></param>
        /// <returns></returns>
        public async Task<HttpStatusCode> ChangePasswort(string userEmail, string oldPassword, string newPassword)
        {
            try
            {
                byte[] oldPasswordBytes = Encoding.UTF8.GetBytes(oldPassword);
                User user = await GetUserByEmail(userEmail);
                if (user != null)
                {
                    //find out the old password and compare it
                    byte[] oldSalt = user.salt;
                    byte[] oldPasswordSalt = GenerateSaltedHash(oldPasswordBytes, oldSalt);
                    bool comparePasswords = CompareByteArrays(oldPasswordSalt, user.passwordSalt);

                    //statement: when users password is correct and status is activated  -> true  
                    if (comparePasswords == true && user.status == true)
                    {
                        //set the new password now and insert into DB
                        byte[] newPasswordBytes = Encoding.UTF8.GetBytes(newPassword);
                        byte[] newSalt = GenerateSalt(saltSize);
                        byte[] newPasswordSalt = GenerateSaltedHash(newPasswordBytes, newSalt);
                        user.passwordSalt = newPasswordSalt;
                        user.salt = newSalt;
                        await userRepo.UpdateAsync(user);
                        return HttpStatusCode.OK;
                    }
                    else
                    {
                        //Change password is not possible because password is false, status is not activated.
                        return HttpStatusCode.BadRequest;
                    }
                }
                else
                {
                    //User is null - User with email do not exists in database.
                    return HttpStatusCode.NotFound;
                }
            }
            catch (TimeoutException e)
            {
                //Database Error
                Console.WriteLine("{0} Exception caught: Database-Timeout. ", e);
                return HttpStatusCode.Conflict;
            }catch(Exception e){
                //Database Error
                Console.WriteLine("{0} Exception caught. ", e);
                return HttpStatusCode.Conflict;
            }

        }

        /// <summary>
        /// Send a confirmation-email to the users email and activate the account (user-status==true).
        /// </summary>
        /// <param name="activationcode"></param>
        /// <returns></returns>
        public async Task ConfirmEmail(string activationcode)
        {
            try
            {
                string messageBody = "Mail confirmed.";
                messageBody += "<br /><br />Thank you for the Registration";

                //Create smtp connection.
                SmtpClient client = new SmtpClient
                {
                    //outgoing port for the mail-server.
                    Port = 587,
                    Host = "smtp.gmail.com",
                    EnableSsl = true,
                    Timeout = 10000,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false,
                    Credentials = new System.Net.NetworkCredential(fromAddress, mailPassword)
                };

                // Fill the mail form.
                var sendMail = new MailMessage();
                sendMail.IsBodyHtml = true;
                //address from where mail will be sent.
                sendMail.From = new MailAddress(fromAddress);
                //address to which mail will be sent.           
                User user = await GetUserbyActivationCode(activationcode);
                sendMail.To.Add(new MailAddress(user.email));
                //subject of the mail.
                sendMail.Subject = "Confirmation: PlaceToBe";
                sendMail.Body = messageBody;
                //Change user status to true, because user is now activated
                await ChangeUserStatus(activationcode);
                
                try
                {
                    //Send the mail 
                    client.Send(sendMail);
                }
                catch (System.Net.Mail.SmtpException cantsend)
                {
                    //Email cannot be sent.
                    Console.WriteLine("{0} Exception caught. Email cannot be sent.", cantsend);
                }
                catch (System.ArgumentNullException messagenull)
                {
                    //Email-message is null
                    Console.WriteLine("{0} Exception caught. Email is null.", messagenull);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("{0} Exception caught. Confirmmail can not be done.", e);
            }
        }


        /// <summary>
        /// Send an email to user, register user with "inactive" status (if the user is 
        /// not already in the DB) 
        /// +Mail id password from where mail will be sent -> At the moment from gmail
        /// with "placetobecologne@gmail.com".
        /// </summary>
        /// <param name="userEmail"></param>
        /// <param name="userPassword"></param>
        /// <returns></returns>
        public async Task<HttpStatusCode> SendActivationEmail(string userEmail, string userPassword)
        {
            try
            {
                //check if user already exists. if the user is null, the user does not exists- so we can send a activationmail
                if (await CheckIfUserExists(userEmail))
                {
                //activationcode to identify the user in the email.
                string activationCode = Guid.NewGuid().ToString();

                string messageBody = "Confirm the mail:";
                messageBody += "<br /><br />Please click the following link to activate your account";
                messageBody += "<br /><a href = ' http://localhost:18172/api/user?activationcode=" + activationCode + "'>Click here to activate your account.</a>";
                messageBody += "<br /><br />Thanks";

                //Create smtp connection.
                SmtpClient client = new SmtpClient();
                //outgoing port for the mail-server.
                client.Port = 587;
                client.Host = "smtp.gmail.com";
                client.EnableSsl = true;
                client.Timeout = 10000;
                client.DeliveryMethod = SmtpDeliveryMethod.Network;
                client.UseDefaultCredentials = false;
                client.Credentials = new System.Net.NetworkCredential(fromAddress, mailPassword);

                // Fill the mail form.
                var sendMail = new MailMessage();
                sendMail.IsBodyHtml = true;
                //address from where mail will be sent.
                sendMail.From = new MailAddress(fromAddress);
                //address to which mail will be sent.           
                sendMail.To.Add(new MailAddress(userEmail));
                //subject of the mail.
                sendMail.Subject = "Registration: PlaceToBe";
                sendMail.Body = messageBody;
                //Register the user with inactive status (status==false)
                
                //if user==null then he does not exists 
                
                byte[] plainText = Encoding.UTF8.GetBytes(userPassword);
                byte[] salt = GenerateSalt(saltSize);
                byte[] passwordSalt = GenerateSaltedHash(plainText, salt);

                User user = new User(userEmail, passwordSalt, salt);
                user.status = false;
                user.activationcode = activationCode;
                await InsertUserToDB(user);
               
                //Send the mail 
                try
                {
                    client.Send(sendMail);
                    return HttpStatusCode.OK; 
                }
                catch (System.Net.Mail.SmtpException cantsend)
                {
                    //email cannot be sent.
                    Console.WriteLine("{0} Exception caught. Email cannot be sent.", cantsend);
                    return HttpStatusCode.BadGateway;
                }
                catch (System.ArgumentNullException messagenull)
                {
                    //Email-message is null
                    Console.WriteLine("{0} Exception caught. Email is null.", messagenull);
                    return HttpStatusCode.BadRequest;
                }
                
                }else {
                //User already exists
                return HttpStatusCode.Forbidden;
                }
              }
            catch ( Exception e)
            {
                Console.WriteLine("{0} Exception caught. ", e);
                return HttpStatusCode.Conflict;
            }
        }


        /// <summary>
        /// Reset the password from the users mail and then send a mail to user with a new password. 
        /// </summary>
        /// <param name="userEmail"></param>
        /// <returns></returns>
        public async Task ForgetPasswordReset(string userEmail)
        {
            try
            {
                User forgetUserPassword = await userRepo.GetByEmailAsync(userEmail);
                byte[] newPassword = Encoding.UTF8.GetBytes(CreateRandomString(8));
                byte[] salt = GenerateSalt(saltSize);
                byte[] passwordSalt = GenerateSaltedHash(newPassword, salt);
                forgetUserPassword.passwordSalt = passwordSalt;
                forgetUserPassword.salt = salt;
                await userRepo.UpdateAsync(forgetUserPassword);
                SendForgetPassword(newPassword, userEmail);

            }
            catch (NullReferenceException usernull)
            {
                Console.WriteLine("{0} ForgetPassword: User is null", usernull);
            }
            catch (TimeoutException e)
            {
                Console.WriteLine("{0} ForgetPassword: Cant update database ", e);
            }
            catch (Exception e)
            {
                Console.WriteLine("{0} Exception caught. ", e);
            }
        }

        /// <summary>
        /// Send a mail with a new password to the users email
        /// </summary>
        /// <param name="bytePassword"></param>
        /// <param name="userEmail"></param>
        public void SendForgetPassword(byte[] bytePassword, string userEmail)
        {
            string passwordString = Encoding.UTF8.GetString(bytePassword);

            string messageBody = "Your new password is: " + passwordString;
            messageBody += "<br /><a href = ' http://localhost:18172/api/login/ '>Click here to login with the new password.</a>";
            messageBody += "<br /><br />Have Fun with placeToBe.";

            //Create smtp connection.
            SmtpClient client = new SmtpClient();
            client.Port = 587; //outgoing port for the mail-server.
            client.Host = "smtp.gmail.com";
            client.EnableSsl = true;
            client.Timeout = 10000;
            client.DeliveryMethod = SmtpDeliveryMethod.Network;
            client.UseDefaultCredentials = false;
            client.Credentials = new System.Net.NetworkCredential(fromAddress, mailPassword);

            // Fill the mail form.
            var sendMail = new MailMessage();
            sendMail.IsBodyHtml = true;
            //address from where mail will be sent.
            sendMail.From = new MailAddress(fromAddress);
            //address to which mail will be sent.           
            sendMail.To.Add(new MailAddress(userEmail));
            //subject of the mail.
            sendMail.Subject = "PlaceToBe: New password";
            sendMail.Body = messageBody;
            //Send the mail 
            try
            {
                client.Send(sendMail);
            }
            catch (System.Net.Mail.SmtpException cantsend)
            {
                //Email cannot be sent.
                Console.WriteLine("{0} Exception caught. Email cannot be sent.", cantsend);
            }
            catch (System.ArgumentNullException messagenull)
            {
                //Email-message is null
                Console.WriteLine("{0} Exception caught. Email is null.", messagenull);
            }
        }

        #region Helper Methods

        /// <summary>
        /// Create a random string from a specific content
        /// </summary>
        /// <param name="length"></param>
        /// <returns>string</returns>
        public string CreateRandomString(int length)
        {
            StringBuilder stringBuilder = new System.Text.StringBuilder();
            string content = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz1234567890";
            Random rnd = new Random();
            for (int i = 0; i < length; i++)
                stringBuilder.Append(content[rnd.Next(content.Length)]);
            return stringBuilder.ToString();
        }

        /// <summary>
        /// Return bytes from a given string
        /// </summary>
        /// <param name="str"></param>
        /// <returns>byte[]</returns>
        static byte[] GetBytes(string str)
        {
            byte[] bytes = new byte[str.Length];
            //System.Buffer.BlockCopy(str.ToCharArray(), 0, bytes, 0, bytes.Length);
            return bytes;
        }
        /// <summary>
        /// Convert a byte-array to a string
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns>string</returns>
        static string GetString(byte[] bytes)
        {
            char[] chars = new char[bytes.Length / sizeof(char)];
            System.Buffer.BlockCopy(bytes, 0, chars, 0, bytes.Length);
            return new string(chars);
        }

        /// <summary>
        /// Change User status from false to true to activate the account.
        /// </summary>
        /// <param name="activationcode"></param>
        /// <returns></returns>

        public async Task ChangeUserStatus(string activationcode)
        {

            User user = await GetUserbyActivationCode(activationcode);
            user.status = true;
            await userRepo.UpdateAsync(user);

        }

        ///<summary>Get User from DB </summary>
        /// <param name="activationcode" </ param>
        /// <returns>returns the users activationcode</returns>
        public async Task<User> GetUserbyActivationCode(string activationcode)
        {

            return await userRepo.GetByActivationCode(activationcode);
        }

        /// <summary>
        /// Get salt from db
        /// </summary>
        /// <param name="email">email of the user</param>
        /// <returns>return the user saved in the db</returns>
        public async Task<User> GetUserByEmail(string email)
        {
            return await userRepo.GetByEmailAsync(email);
        }

        private async Task<Guid> InsertUserToDB(User user)
        {
            return await userRepo.InsertAsync(user);
        }

        public async Task<Boolean> CheckIfUserExists(string userEmail)
        {
            if (await GetUserByEmail(userEmail) == null)
            {
                return true;
            }
            else { return false;
            }
        }
        /// <summary>
        /// Generate Salt
        /// </summary>
        /// <returns>Salt</returns>
        private byte[] GenerateSalt(int saltSize)
        {
            byte[] saltCrypt = new byte[saltSize];
            RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
            rng.GetBytes(saltCrypt);
            return saltCrypt;
        }

        /// <summary>
        /// Generate a "salted" Hashcode from a given plaintext and a salt.
        /// </summary>
        /// <param name="plainText">password text</param>
        /// <param name="salt">generated Salt</param>
        /// <returns>Value with plaintext+salt</returns>
        private byte[] GenerateSaltedHash(byte[] plainText, byte[] salt)
        {
            HashAlgorithm algorithm = new SHA256Managed();

            byte[] plainTextWithSaltBytes =
              new byte[plainText.Length + salt.Length];

            for (int i = 0; i < plainText.Length; i++)
            {
                plainTextWithSaltBytes[i] = plainText[i];
            }
            for (int i = 0; i < salt.Length; i++)
            {
                plainTextWithSaltBytes[plainText.Length + i] = salt[i];
            }

            return algorithm.ComputeHash(plainTextWithSaltBytes);
        }

        /// <summary>
        /// Compare two byte-arrays.
        /// </summary>
        /// <param name="array1"></param>
        /// <param name="array2"></param>
        /// <returns></returns>
        public static bool CompareByteArrays(byte[] array1, byte[] array2)
        {
            if (array1.Length != array2.Length)
            {
                return false;
            }

            for (int i = 0; i < array1.Length; i++)
            {
                if (array1[i] != array2[i])
                {
                    return false;
                }
            }

            return true;
        }
        #endregion
    }

}