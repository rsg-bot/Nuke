using Nuke.Common;
#pragma warning disable CA1822

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