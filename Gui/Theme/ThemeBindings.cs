/*
Copyright (c) 2026, Lars Brubaker
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// Records which widget properties were copied from which theme colour, so a widget that is up when the
	/// theme changes in place (<see cref="ThemeConfig.Changed"/>) can take the new colours.
	/// </summary>
	/// <remarks>
	/// Menu rows copy the theme when they are built, and a menu stays open while a theme is picked from it
	/// (View > Color). agg-gui paints its menus from the current visuals every frame; here each copy site binds
	/// the property to its theme role instead, and <see cref="Refresh"/> re-applies the roles. A binding is
	/// re-applied only while the property still holds the value the binding last wrote, so a caller that set
	/// the property itself afterwards keeps its value, and properties nobody bound are never touched.
	/// </remarks>
	internal static class ThemeBindings
	{
		private static readonly ConditionalWeakTable<GuiWidget, List<IBinding>> Bindings = new ConditionalWeakTable<GuiWidget, List<IBinding>>();

		/// <summary>The popups currently following their theme, so a popup shown again is not subscribed twice.</summary>
		private static readonly ConditionalWeakTable<GuiWidget, EventHandler> Followed = new ConditionalWeakTable<GuiWidget, EventHandler>();

		private interface IBinding
		{
			string Property { get; }

			ThemeConfig Theme { get; }

			void Refresh();
		}

		/// <summary>
		/// Sets a property of <paramref name="widget"/> to <paramref name="role"/> of <paramref name="theme"/> now,
		/// and again on every <see cref="Refresh"/> until something else changes it. Binding the same
		/// <paramref name="property"/> again (a subclass restyling what its base set) replaces the earlier binding.
		/// </summary>
		public static void Bind<T>(GuiWidget widget, string property, ThemeConfig theme, Func<ThemeConfig, T> role, Func<T> get, Action<T> set)
		{
			var binding = new Binding<T>(property, theme, role, get, set);
			var list = Bindings.GetOrCreateValue(widget);
			list.RemoveAll(b => b.Property == property);
			list.Add(binding);
		}

		/// <summary><see cref="Bind"/> for a widget's background.</summary>
		public static void BindBackground(GuiWidget widget, ThemeConfig theme, Func<ThemeConfig, Color> role)
		{
			Bind(widget, nameof(GuiWidget.BackgroundColor), theme, role, () => widget.BackgroundColor, c => widget.BackgroundColor = c);
		}

		/// <summary><see cref="Bind"/> for a label's text colour.</summary>
		public static void BindTextColor(TextWidget text, ThemeConfig theme, Func<ThemeConfig, Color> role)
		{
			Bind(text, nameof(TextWidget.TextColor), theme, role, () => text.TextColor, c => text.TextColor = c);
		}

		/// <summary>
		/// Re-applies every binding to <paramref name="theme"/> on <paramref name="root"/> and its descendants.
		/// </summary>
		public static void Refresh(GuiWidget root, ThemeConfig theme)
		{
			foreach (var widget in new[] { root }.Concat(root.Descendants<GuiWidget>()))
			{
				if (Bindings.TryGetValue(widget, out var list))
				{
					foreach (var binding in list.Where(b => b.Theme == theme).ToList())
					{
						binding.Refresh();
					}

					widget.Invalidate();
				}
			}
		}

		/// <summary>
		/// Keeps <paramref name="popup"/> in step with <paramref name="theme"/> from now until it closes. A popup
		/// that is shown again while still followed is not subscribed a second time.
		/// </summary>
		public static void FollowWhileOpen(GuiWidget popup, ThemeConfig theme)
		{
			if (Followed.TryGetValue(popup, out _))
			{
				return;
			}

			EventHandler refresh = (s, e) => Refresh(popup, theme);
			Followed.Add(popup, refresh);
			theme.Changed += refresh;

			EventHandler closed = null;
			closed = (s, e) =>
			{
				theme.Changed -= refresh;
				Followed.Remove(popup);
				popup.Closed -= closed;
			};
			popup.Closed += closed;
		}

		private sealed class Binding<T> : IBinding
		{
			private readonly Func<ThemeConfig, T> role;
			private readonly Func<T> get;
			private readonly Action<T> set;
			private T written;

			public Binding(string property, ThemeConfig theme, Func<ThemeConfig, T> role, Func<T> get, Action<T> set)
			{
				this.Property = property;
				this.Theme = theme;
				this.role = role;
				this.get = get;
				this.set = set;

				this.written = role(theme);
				set(this.written);
			}

			public string Property { get; }

			public ThemeConfig Theme { get; }

			public void Refresh()
			{
				// Someone else has set it since: theirs wins, and this binding is done with it
				if (!Same(get(), written))
				{
					return;
				}

				var value = role(Theme);
				if (!Same(value, written))
				{
					written = value;
					set(value);
				}
			}

			// An image is compared by reference: a fresh icon is a new value even when its pixels match
			private static bool Same(T a, T b) => typeof(T).IsValueType ? EqualityComparer<T>.Default.Equals(a, b) : ReferenceEquals(a, b);
		}
	}
}
