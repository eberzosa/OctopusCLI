﻿namespace Octopus.Client.Model
{
    public class FeaturesConfigurationResource : Resource
    {
        [Writeable]
        public bool IsMultiTenancyEnabled { get; set; }
        [Writeable]
        public bool IsCommunityActionTemplatesEnabled { get; set; }
    }
}
