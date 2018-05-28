﻿using System;

namespace FeatureAdmin.Core.Messages.Completed
{
    public class FeatureDeactivationCompleted : BaseFeatureToggleCompleted
    {
        /// <summary>
        /// provides the loaded location including child locations
        /// </summary>
        /// <param name="taskId">reference to task id</param>
        /// <param name="locationReference">where did action take place</param>
        /// <param name="error">was activation successful?</param>
        public FeatureDeactivationCompleted(
            Guid taskId
            , Guid locationReference
            , string error = null
            ) : base (taskId, locationReference, error)
        {
            
         }
    }
}
