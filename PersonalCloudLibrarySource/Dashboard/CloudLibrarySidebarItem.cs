using Playnite.SDK;
using Playnite.SDK.Plugins;
using System;
using System.Windows.Controls;

namespace PersonalCloudLibrarySource
{
    public sealed class CloudLibrarySidebarItem : SidebarItem
    {
        public CloudLibrarySidebarItem(Func<Control> viewFactory, string iconPath)
        {
            if (viewFactory == null)
            {
                throw new ArgumentNullException(nameof(viewFactory));
            }

            Type = SiderbarItemType.View;
            Title = ResourceProvider.GetString("LOCPLSSidebarTitle") ?? "Cloud Library";
            Icon = iconPath;
            Visible = true;
            Opened = viewFactory;
        }
    }
}
