﻿using placeToBe.Model;
using placeToBe.Model.Entities;
using placeToBe.Model.Repositories;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;
using System.Web.Helpers;

namespace placeToBe.Services
{
    public class GenderizeService
    {

        public String result;
        public String name;
        public String gender;

        MongoDbRepository<Gender> repo = new MongoDbRepository<Gender>();

        public String URL { get; set; }

        /// <summary>
        /// GetGender uses the genderize.io API to get the gender of a prename
        /// </summary>
        /// <param name="name">using a name of a person to get the gender</param>
        public void SetGender(String name)
        {
            HttpWebRequest request;
            String getData = "name=" + name;
            URL = "http://api.genderize.io/?";
            Uri uri = new Uri(URL + getData);
            request = (HttpWebRequest)WebRequest.Create(uri);

            request.Method = "GET";

            request.AllowAutoRedirect = true;

            UTF8Encoding enc = new UTF8Encoding();

            HttpWebResponse Response;
            try
            {
                using (Response = (HttpWebResponse)request.GetResponse())
                {
                    using (Stream responseStream = Response.GetResponseStream())
                    {
                        using (StreamReader readStream = new StreamReader(responseStream, Encoding.UTF8))
                        {
                            this.result = readStream.ReadToEnd();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error: " + ex.Message);
                throw ex;
            }
        }

        public void GenderToObject(string result)
        {
            String json = @result;
            Gender gender = new Gender(json);
        }

        public void PushGenderToDb(String name)
        {
            SetGender(name);
           

        }

        public void GenderStat(Event eventGenStat)
        {

        }
    }
}