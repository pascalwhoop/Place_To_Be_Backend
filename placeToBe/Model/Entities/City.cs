﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using MongoDB.Bson.Serialization.Attributes;

namespace placeToBe.Model.Entities
{
    /// <summary>
    /// class includes a City represented by its name and coordinates 
    /// (coordinates build quader which includes the city).
    /// </summary>
    public class City : EntityBase
    {
        
        public String name { get; set; }
        public Coordinates[][] area { get; set; }


    }

    public class Coordinates
    {
        public double latitude { get; set; }
        public double longitude { get; set; }
    }

}