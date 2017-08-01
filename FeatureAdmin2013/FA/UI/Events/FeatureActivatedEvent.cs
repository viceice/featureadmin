﻿using FA.Models;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FA.UI.Events
{
    public class FeatureActivatedEvent : PubSubEvent<ActivatedFeature>
    {
    }
}
