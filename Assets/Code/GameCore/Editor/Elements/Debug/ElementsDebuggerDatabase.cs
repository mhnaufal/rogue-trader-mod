using System;
using System.Collections.Generic;
using Code.GameCore.ElementsSystem;
using ElementsSystem.Debug;
using JetBrains.Annotations;
using Kingmaker.Blueprints;
using Kingmaker.ElementsSystem;
using Kingmaker.Utility.DotNetExtensions;
using Kingmaker.Utility.UnityExtensions;
using Owlcat.Runtime.Core.Utility;
using UnityEditor;

namespace Code.GameCore.Editor.Elements.Debug
{
	[InitializeOnLoad]
	public static class ElementsDebuggerDatabase
	{
		public const int ElementsPerPage = 10;
		
		public interface IEntry
		{
			SimpleBlueprint Asset { get; }

			int? LastResult { get; }

			Exception LastException { get; }

			string Name { get; }

			string LowerInvariantName { get; }
            
            public ContextDebugData ContextDebugData { get; set; }
		}

		public class AssetEntry : IEntry
		{
			public SimpleBlueprint Asset;

			public readonly HashSet<int> Lists = new();

			public readonly HashSet<Element> Elements = new();
			
			public readonly List<Exception> Exceptions = new();

			SimpleBlueprint IEntry.Asset
				=> Asset;

			int? IEntry.LastResult
				=> null;

			[CanBeNull]
			public Exception LastException
				=> Exceptions.LastItem();

			public string Name
				=> Asset.name;

			public string LowerInvariantName
				=> Name.ToLowerInvariant();
            
            public ContextDebugData ContextDebugData { get; set; }
		}

		public class ListEntry : IEntry
		{
			[CanBeNull]
			public SimpleBlueprint Asset;

			public int ID;

			public readonly List<int> Results = new();
			
			public readonly List<Exception> Exceptions = new();

			public readonly HashSet<Element> Elements = new();

			SimpleBlueprint IEntry.Asset
				=> Asset;

			public int? LastResult
				=> Results.Count > 0 ? Results[^1] : null;

			[CanBeNull]
			public Exception LastException
				=> Exceptions.LastItem();

			public string Name
				=> Asset?.name ?? "";

			public string LowerInvariantName
				=> Name.ToLowerInvariant();
            
            public ContextDebugData ContextDebugData { get; set; }
		}

		public class ElementEntry : IEntry
		{
			public int? ListId;
			public Element Element;

			public readonly List<int> Results = new();
			
			public readonly List<Exception> Exceptions = new();

			public SimpleBlueprint Asset
				=> Element.Owner;

			public int? LastResult
				=> Results.Count > 0 ? Results[^1] : null;

			[CanBeNull]
			public Exception LastException
				=> Exceptions.LastItem();

			public string Name
				=> Element.name;

			public string LowerInvariantName
				=> Element.name.ToLowerInvariant();
            
            public ContextDebugData ContextDebugData { get; set; }
		}

		public class ElementLogEntry
		{
			public Element Element;
			public DateTime Time;
			public int Depth;
			public int? Result;
			[CanBeNull]
			public Exception Exception;
		}

		static ElementsDebuggerDatabase()
		{
			ElementsDebugger.EnterCallback += Enter;
			ElementsDebugger.ExitCallback += Exit;
			ElementsDebugger.LogCallback += (_, _) => {};
			ElementsDebugger.ClearExceptionCallback += ClearException;
		}

		private static readonly Dictionary<string, AssetEntry> Assets = new();
		private static readonly Dictionary<int, ListEntry> Lists = new();
		private static readonly Dictionary<string, ElementEntry> Elements = new();

		private static readonly List<ElementLogEntry> ElementsLogStack = new();
		public static readonly List<ElementLogEntry> ElementsLog = new();

		public static bool ExceptionsOnly { get; set; } = true;

		[CanBeNull]
		public static AssetEntry Get([CanBeNull] SimpleBlueprint asset)
			=> asset == null ? null : Assets.Get(asset.AssetGuid);

		[CanBeNull]
		public static ListEntry Get([CanBeNull] ElementsList list)
			=> list == null ? null : Lists.Get(list.GetID());

		[CanBeNull]
		public static ElementEntry Get([CanBeNull] Element e)
			=> e == null ? null : Elements.Get(e.name);

		public static ReadonlyList<T> SelectEntries<T>(
			[NotNull] Dictionary<string, T> storage,
			[CanBeNull] SimpleBlueprint filterAsset,
			[CanBeNull] string filterString,
			ref int page,
			out int pagesCount) where T : IEntry
		{
			bool Match(T entry, SimpleBlueprint bp, string s)
				=> (bp == null || bp == entry.Asset) &&
				   (s.IsNullOrEmpty() || s.ToLowerInvariant().Split(' ').HasItem(entry.LowerInvariantName.Contains)) &&
				   (!ExceptionsOnly || entry.LastException != null);

			if (page < 0)
				page = 0;
				
			var result = TempList.Get<T>();

			int matchesCount = 0;
			foreach (var i in storage)
			{
				bool match = Match(i.Value, filterAsset, filterString);
				if (!match)
					continue;
				
				matchesCount += 1;

				int currentPage = (matchesCount - 1) / ElementsPerPage;
				if (page == currentPage)
					result.Add(i.Value);
			}

			pagesCount = matchesCount / ElementsPerPage + (matchesCount % ElementsPerPage == 0 ? 0 : 1);
			if (page >= pagesCount)
				page = pagesCount - 1;

			return result;
		}

		public static ReadonlyList<ElementEntry> SelectElements(
			[CanBeNull] SimpleBlueprint filterAsset,
			[CanBeNull] string filterString,
			ref int page,
			out int pagesCount)
			=> SelectEntries(Elements, filterAsset, filterString, ref page, out pagesCount);

		public static ReadonlyList<AssetEntry> SelectAssets(
			[CanBeNull] string filterString,
			ref int page,
			out int pagesCount)
			=> SelectEntries(Assets, null, filterString, ref page, out pagesCount);

		public static void Clear()
		{
			Assets.Clear();
			Elements.Clear();
			Lists.Clear();
		}

		private static void Enter(ElementsDebugger debugger)
		{
			if (debugger.Element is {} element)
			{
				if (ElementsDebugger.LogEnabled)
				{
					var entry = new ElementLogEntry
					{
						Element = element,
						Depth = ElementsLogStack.Count,
						Time = DateTime.Now,
					};

					ElementsLog.Add(entry);
					ElementsLogStack.Add(entry);
				}

				var elementEntry = Elements.Get(element.name) ?? (Elements[element.name] = new ElementEntry {Element = element});
				elementEntry.ListId = debugger.List?.GetID();
			}

			if (debugger.List is {} list)
			{
				var listEntry = Lists.Get(list.GetID()) ?? (Lists[list.GetID()] = new ListEntry {ID = list.GetID()});
				listEntry.Asset ??= debugger.Element?.Owner;
				if (debugger.Element is {} e)
					listEntry.Elements.Add(e);
			}

			if (debugger.Element?.Owner is {} asset)
			{
				var assetEntry = Assets.Get(asset.AssetGuid) ?? (Assets[asset.AssetGuid] = new AssetEntry {Asset = asset});
				if (debugger.List is {} l)
					assetEntry.Lists.Add(l.GetID());
				if (debugger.Element is {} e)
					assetEntry.Elements.Add(e);

			}
		}

		private static void Exit(ElementsDebugger debugger)
		{
			if (debugger.Element is {} element)
			{
				if (ElementsDebugger.LogEnabled)
				{
					var elementEntry = ElementsLogStack.LastItem();
					if (elementEntry?.Element != element)
						Element.LogError("Elements Debugger stack is broken");

					if (elementEntry != null)
					{
						elementEntry.Result = debugger.Result;
						elementEntry.Exception = debugger.Exception;
					}
					
					ElementsLogStack.RemoveLast();
				}

				var entry = Elements.Get(element.name);
				if (entry != null)
				{
                    if (debugger.ContextDebugData != null)
                        entry.ContextDebugData = debugger.ContextDebugData;
					if (debugger.Exception != null)
						entry.Exceptions.Add(debugger.Exception);
					else if (debugger.Result != null)
						entry.Results.Add(debugger.Result.Value);
				}
			}

			if (debugger.List is {} list)
			{
				var listEntry = Lists.Get(list.GetID());
				if (listEntry != null)
				{
                    if (debugger.ContextDebugData != null)
                        listEntry.ContextDebugData = debugger.ContextDebugData;
					if (debugger.Exception is {} exception)
						listEntry.Exceptions.Add(exception);
					else if (debugger.Result is {} result)
						listEntry.Results.Add(result);
				}
			}

			if (debugger.Element?.Owner is {} asset)
			{
				var assetEntry = Assets.Get(asset.AssetGuid);
				if (assetEntry != null)
				{
					if (debugger.Exception is {} exception)
						assetEntry.Exceptions.Add(exception);
				}
			}
		}

		private static void ClearException(Element element, Exception exception)
		{
			// doesn't work properly
			// Get(element)?.Exceptions?.RemoveAll(i => i == exception || i == exception.InnerException);
			// Get(element.Owner)?.Exceptions.RemoveAll(i => i == exception || i == exception.InnerException);
			//
			// var lastLog = ElementsLog.LastItem();
			// if (lastLog != null &&
			//     lastLog.Element == element &&
			//     (lastLog.Exception == exception || lastLog.Exception == exception.InnerException))
			// {
			// 	lastLog.Exception = null;
			// }
		}
	}
}