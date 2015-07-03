﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Driver;
using Newtonsoft.Json;
using placeToBe.Model.Entities;
using placeToBe.Model.Repositories;

namespace placeToBe.Services
{
    public class GenderizeService
    {
        EventRepository repoEvent = new EventRepository();
        GenderRepository repoGender = new GenderRepository();
        public DateTime lastRequest;
        public int xRateLimitRemaining;
        public int xRateReset;
        public string url { get; set; }

        public List<string> getPrenamesStringArray(List<Rsvp> rsvpArray)
        {
            List<String> onlyPrenameList = new List<String>();
            String[] splitItem;
            
            foreach (var item in rsvpArray)
            {
                splitItem = item.name.Split(new[] {" ", "-"}, StringSplitOptions.None);
                onlyPrenameList.Add(splitItem[0]);
            }
            return onlyPrenameList;
        }

        /// <summary>
        ///     return the gender statistik of a specific event
        /// </summary>
        /// <param name="fbId">id of an event</param>
        /// <returns>return an int[] array with value of array[0]=male, array[1]=female, array[2]=undefined</returns>
        //public async Task<Event> getGenderStat(Event newEvent)
        //{
        //    var genderStat = new int[3];
        //        genderStat = await createGenderStat(newEvent);

        //        genderStat[0] = newEvent.attendingMale;
        //        genderStat[1] = newEvent.attendingFemale;
        //        genderStat[2] = newEvent.attendingUndefined;

        //    return ;
        //}

        /// <summary>
        ///     Search for a gender by name and returns it.
        /// </summary>
        /// <param name="name">name of a person</param>
        /// <returns>gender of the name</returns>
        public async Task<Gender> getGender(string name)
        {
            Gender gender;
            try
            {
                gender = await searchDbForGender(name);
            }
            catch (Exception e)
            {
                Console.WriteLine("{0} Exception caught.", e);
                gender = null;
            }

            if (gender == null)
            {
                gender = getGenderFromApi(name);
                Debug.WriteLine("######## Got it from Api");
                if (gender != null) pushGenderToDb(gender);
                
            }
            else
            {
                Debug.WriteLine("######### Already in DB");
            }

            return gender;
        }

        /// <summary>
        ///     GetGender uses the genderize.io API to get the gender of a prename
        /// </summary>
        /// <param name="name">using a name of a person to get the gender</param>
        public Gender getGenderFromApi(string name)
        {
            string result;
            Gender gender = null;

            var getData = "name=" + name;
            url = "http://api.genderize.io/?";
            var uri = new Uri(url + getData);
            var request = (HttpWebRequest) WebRequest.Create(uri);

            request.Method = "GET";

            request.AllowAutoRedirect = true;

            new UTF8Encoding();


            HttpWebResponse response;
            try
            {
                using (response = (HttpWebResponse) request.GetResponse())
                {
                    using (var responseStream = response.GetResponseStream())
                    {
                        using (var readStream = new StreamReader(responseStream, Encoding.UTF8))
                        {
                            xRateLimitRemaining = int.Parse(response.Headers["X-Rate-Limit-Remaining"]);
                            xRateReset = int.Parse(response.Headers["X-Rate-Reset"]);
                            lastRequest = DateTime.Now;

                            //String of the json from genderize.io
                            result = readStream.ReadToEnd();
                            gender = JsonConvert.DeserializeObject<Gender>(result);
                            if (gender.gender == null)
                            {
                                gender.gender = "undefined";
                            }

                            return gender;
                        }
                    }
                }
            }
            catch (WebException webEx)
            {
                if (xRateLimitRemaining == 0)
                {
                    /*if (xRateReset == 0)
                    {
                        var difference = (DateTime.Now.AddDays(1) - DateTime.Now).TotalSeconds;
                        //round and seconds to milliseconds
                        var differenceInt = (Convert.ToInt32(Math.Floor(difference)))*1000;
                        Debug.WriteLine("####### Waiting " + differenceInt);
                        Thread.Sleep(differenceInt);
                    }
                    else
                    {
                        var difference = (DateTime.Now - lastRequest).TotalSeconds;
                        //round and seconds to milliseconds
                        var differenceInt = (Convert.ToInt32(Math.Floor(difference)));

                        var sleepDifference = (xRateReset - differenceInt)*1000;
                        Debug.WriteLine("####### Waiting " + sleepDifference);
                        Thread.Sleep(sleepDifference);
                    }*/
                    //return getGenderFromApi(name);
                    return null;
                }
                Debug.WriteLine("Error: " + webEx.Message);
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error: " + ex.Message);
                if (gender != null)
                {
                    gender.name = name;
                    gender.gender = "undefined";
                    gender.count = 0;
                    gender.probability = 0;
                    return gender;
                }
                    throw;
                }
        }

        /// <summary>
        ///     Get the amount of males and females for an event
        /// </summary>
        /// <returns>returns array with array[0]=male, array[1]=female, array[2]=undefined</returns>
        public async Task<Event> createGenderStat(Event newEvent)
        {
            var male = 0;
            var female = 0;
            var undefined = 0;

            Gender gender;


            //GET list of people attending the event
            var attendingList = newEvent.attending;
            var preNameList = getPrenamesStringArray(attendingList);

            foreach (var name in preNameList)
            {
                gender = await getGender(name);
                if (gender == null) {
                    undefined++;
                    continue;
                }
                if (gender.gender == "male")
                {
                    male++;
                }
                else if (gender.gender == "female")
                {
                    female++;
                }
                else
                {
                    undefined++;
                }
            }

            newEvent.attendingMale = male;
            newEvent.attendingFemale = female;
            newEvent.attendingUndefined = undefined;

            return newEvent;
        }

        #region HelperMethods

        //private Gender GenderToObject(String result)
        //{
        //    String json = @result;
        //    Gender gender = new Gender(json);
        //    return gender;
        //}

        private async void updateGenderStat(Event eventNew)
        {
            try
            {
                await repoEvent.UpdateAsync(eventNew);
            }
            catch (MongoWriteException e)
            {
                Console.Write(e.Message);
            }
            catch (MongoWaitQueueFullException ex)
            {

                Console.WriteLine("{0} Exception caught.", ex);
                Thread.Sleep(15000);
                updateGenderStat(eventNew);
            }
        }

        private async void pushGenderToDb(Gender gender)
        {
            try
            {
                await repoGender.InsertAsync(gender);
            }
            catch (MongoWriteException e)
            {
                Console.Write(e.Message);
            }
            catch (MongoWaitQueueFullException ex)
            {
                Console.WriteLine("{0} Exception caught.", ex);
                Thread.Sleep(15000);
                pushGenderToDb(gender);
            }
        }

        private async Task<Event> searchDbForEvent(string fbId)
        {
            return await repoEvent.GetByFbIdAsync(fbId);
        }

        public async Task<Gender> searchDbForGender(string name)
        {
            return await repoGender.GetByNameAsync(name);
        }

        #endregion
    }
}