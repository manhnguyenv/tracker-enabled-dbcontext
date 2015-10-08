﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TrackerEnabledDbContext.Common.Testing.Models
{
    [TrackChanges]
    public class TrackedModelWithMultipleProperties
    {
        public int Id { get; set; }

        public string Name { get; set; }

        [SkipTracking]
        public DateTime? StartDate { get; set; }

        [SkipTracking]
        public int Value { get; set; }

        public char Category { get; set; }

        public bool IsSpecial { get; set; }

        [SkipTracking]
        public string Description { get; set; }
    }
}
