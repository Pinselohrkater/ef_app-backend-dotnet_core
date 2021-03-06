﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

namespace Eurofurence.App.Domain.Model.Maps
{
    [DataContract]
    public class MapRecord : EntityBase
    {
        public MapRecord()
        {
            Entries = new List<MapEntryRecord>();
        }

        [DataMember]
        [Required]
        public Guid ImageId { get; set; }

        [DataMember]
        [Required]
        public string Description { get; set; }

        [DataMember]
        [Required]
        public bool IsBrowseable { get; set; }

        [DataMember]
        [Required]
        public IList<MapEntryRecord> Entries { get; set; }
    }
}