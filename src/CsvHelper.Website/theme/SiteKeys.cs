namespace Docable
{
    public static class SiteKeys
    {
		/// <summary>
		/// The title of the site. This should be defined regardless of whether
		/// <see cref="Logo"/> is because it's used for the page title, alt attributes, etc.
		/// </summary>
	    public const string SiteTitle = nameof(SiteTitle);

	    /// <summary>
	    /// The logo file to use in the navigation bar (include a leading slash
	    /// if providing a relative path). If not provided, the <see cref="SiteTitle"/> will be used.
	    /// </summary>
	    public const string Logo = nameof(Logo);

		/// <summary>
		/// Set to <c>false</c> to hide the page in the top navigation.
		/// </summary>
	    public const string ShowInNavigation = nameof(ShowInNavigation);

		/// <summary>
		/// Set to <c>false</c> to hide the page in the sidebar navigation.
		/// </summary>
		public const string ShowInSidebar = nameof(ShowInSidebar);

		/// <summary>
		/// The title of a page when displayed in the navigation, otherwise the normal title will be used.
		/// </summary>
		public const string NavigationTitle = nameof(NavigationTitle);

		/// <summary>
		/// Set to <c>true</c> to hide the page container (margins, sidebars, etc.) for a given page.
		/// </summary>
        public const string NoContainer = nameof(NoContainer);

		/// <summary>
		/// Set to <c>true</c> to hide the sidebar for a given page.
		/// </summary>
        public const string NoSidebar = nameof(NoSidebar);

		/// <summary>
		/// Set to <c>true</c> to hide the title area for a given page.
		/// </summary>
		public const string NoTitle = nameof(NoTitle);

		/// <summary>
		/// Set to <c>true</c> to hide the child pages section at the bottom of a given page.
		/// </summary>
        public const string NoChildPages = nameof(NoChildPages);

		/// <summary>
		/// The title of a page when displayed in the breadcrumbs, otherwise the normal title will be used.
		/// </summary>
        public const string BreadcrumbTitle = nameof(BreadcrumbTitle); //

		/// <summary>
		/// A more specific editing link that overrides <see cref="EditRoot"/> if needed.
		/// </summary>
		public const string EditLink = nameof(EditLink);

		/// <summary>
		/// The root link to use for editing pages, usually set to a value like
		/// "https://github.com/org/repo/edit/develop/input" (do not use a trailing slash).
		/// </summary>
		public const string EditRoot = nameof(EditRoot);

		/// <summary>
		/// A Google Fonts (or other) URL for use in a <c>link</c> element in the header.
		/// </summary>
		public const string FontLink = nameof(FontLink);
    }
}