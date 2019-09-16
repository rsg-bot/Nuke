using System;
using System.Collections.Generic;
using System.Text;
using Nuke.Common;

namespace Rocket.Surgery.Nuke.Xamarin
{
    /// <summary>
    /// Base build plan for Xamarin.Forms based applications
    /// </summary>
    public class FormsBuild : XamarinBuild
    {
        /// <summary>
        /// Core target that can be used to trigger all targets for this build
        /// </summary>
        public Target XamarinForms => _ => _;
    }
}
