using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GreatClock.Common.ResourcesHolder {

	public partial class ResourcesHolder {

		public static bool Init(IAsyncLoader loader) {
			if (loader == null) { return false; }
			if (async_loader != null) { return false; }
			async_loader = loader;
			return true;
		}

		private static IAsyncLoader async_loader;

		private static Transform sCachedGameobjectRoot;

		public delegate void OnResourcesLoadedDelegate<T>(T obj);

		public ResourcesHolder() { }

		public void GetResources<T>(string path, OnResourcesLoadedDelegate<T> callback) where T : Object {
			GetResourcesInternal(null, path, typeof(T), callback);
		}

		public void GetResources<T>(string folder, string file, OnResourcesLoadedDelegate<T> callback) where T : Object {
			GetResourcesInternal(folder, file, typeof(T), callback);
		}

		public void GetResources(string path, Type type, OnResourcesLoadedDelegate<Object> callback) {
			GetResourcesInternal(null, path, type, callback);
		}

		public T GetResourcesSync<T>(string path) where T : Object {
			return GetResourcesInternalSync<T>(null, path, typeof(T));
		}

		public T GetResourcesSync<T>(string folder, string file) where T : Object {
			return GetResourcesInternalSync<T>(folder, file, typeof(T));
		}

		public Object GetResourcesSync(string path, Type type) {
			return GetResourcesInternalSync<Object>(null, path, type);
		}

		public bool ReleaseResources<T>(T obj) where T : Object {
			return ReleaseResourcesInternal(typeof(T), obj);
		}

		public bool ReleaseResources(Type type, Object obj) {
			return ReleaseResourcesInternal(type, obj);
		}

		public bool ReleaseUnused() {
			bool b1 = ReleaseUnusedInstancesInternal();
			bool b2 = ReleaseUnusedResourcesInternal();
			return b1 && b2;
		}

		private class ResourcesData {
			public Type type;
			public Object obj;
			public int refCount;

			public void Reset() {
				type = null;
				obj = null;
				refCount = 0;
			}
		}

		private static Transform GetCachedGameObjectRoot() {
			if (sCachedGameobjectRoot == null) {
				GameObject go = new GameObject("Resources Holder Cache");
				Object.DontDestroyOnLoad(go);
				go.SetActive(false);
				sCachedGameobjectRoot = go.transform;
			}
			return sCachedGameobjectRoot;
		}

		private static Queue<LoadingData> cached_loading_lists = new Queue<LoadingData>();

		private static Queue<ResourcesData> cached_resource_datas = new Queue<ResourcesData>();
		private static List<int> to_remove_list = new List<int>();

		private Dictionary<int, LoadingData> mLoadings = new Dictionary<int, LoadingData>();

		private Dictionary<long, int> mResourcesToPath = new Dictionary<long, int>();

		private void GetResourcesInternal<T>(string folder, string file, Type type, OnResourcesLoadedDelegate<T> callback) where T : Object {
			if (string.IsNullOrEmpty(file)) { return; }
			if (type == null) { return; }
			if (callback == null) { return; }
			int key = HashString.ComputeHash(folder, file);
			ResourcesData data;
			if (mLoaded.TryGetValue(key, out data)) {
				data.refCount++;
				T target = data.obj as T;
				try {
					callback(target);
				} catch (Exception e) {
					Debug.LogException(e);
				}
				return;
			}
			LoadingData callbacks;
			bool isLoading = GetLoadDelegateList(key, out callbacks);
			callbacks.AddCallback(CallbackHolderBase.Get<T>().SetCallback(callback));
			if (isLoading) { return; }
			callbacks.key = key;
			callbacks.type = type;
			string path = folder == null ? file : (folder + file);
			async_loader.Load(path, type, callbacks.onLoaded);
		}

		private T GetResourcesInternalSync<T>(string folder, string file, Type type) where T : Object {
			if (string.IsNullOrEmpty(file)) { return null; }
			if (type == null) { return null; }
			int key = HashString.ComputeHash(folder, file);
			ResourcesData data;
			if (!mLoaded.TryGetValue(key, out data)) { return null; }
			data.refCount++;
			T target = data.obj as T;
			return target;
		}

		private bool GetLoadDelegateList(int key, out LoadingData callbackList) {
			callbackList = null;
			//List<OnResourcesLoadedDelegate<Object>> callbacks;
			if (mLoadings.TryGetValue(key, out callbackList)) {
				return true;
			}
			callbackList = 0 < cached_loading_lists.Count ? cached_loading_lists.Dequeue() : new LoadingData();
			callbackList.onPreCallbacks = OnLoaded1;
			callbackList.onPostCallbacks = OnLoaded2;
			mLoadings.Add(key, callbackList);
			callbackList.Clear();
			return false;
		}

		private void OnLoaded1(LoadingData loadingData, Object obj, int count) {
			if (obj == null) {
				Debug.LogErrorFormat("[ResourcesHolder] Fail to load file '{0}' !", HashString.GetString(loadingData.key));
				return;
			}
			ResourcesData data = CreateResourcesData(loadingData.type, obj);
			data.refCount = count;
			mLoaded.Add(loadingData.key, data);
			mResourcesToPath.Add(GetResourcesKey(loadingData.type, obj), loadingData.key);
		}

		private void OnLoaded2(LoadingData loadingData, Object obj) {
			int key = loadingData.key;
			bool flag = mLoadings.Remove(key);
			loadingData.Clear();
			cached_loading_lists.Enqueue(loadingData);
			if (!flag) {
				Debug.LogErrorFormat("[ResourcesHolder] in OnLoaded() No callbacks found for '{0}' !",
					HashString.GetString(key));
			}
		}

		private static ResourcesData CreateResourcesData(Type type, Object obj) {
			ResourcesData ret = cached_resource_datas.Count > 0 ? cached_resource_datas.Dequeue() : new ResourcesData();
			ret.type = type;
			ret.obj = obj;
			ret.refCount = 0;
			return ret;
		}

		private bool ReleaseResourcesInternal(Type type, Object obj) {
			if (obj == null) { return false; }
			int key;
			if (!mResourcesToPath.TryGetValue(GetResourcesKey(type, obj), out key)) {
				Debug.LogWarningFormat("[ResourcesHolder] ReleaseResourcesInternal() resource '{0}' not managed !", obj);
				return false;
			}
			ResourcesData data;
			if (!mLoaded.TryGetValue(key, out data)) {
				Debug.LogWarningFormat("[ResourcesHolder] ReleaseResourcesInternal() resource '{0}' not managed !", HashString.GetString(key));
				return false;
			}
			data.refCount--;
			return true;
		}

		private bool ReleaseUnusedResourcesInternal() {
			to_remove_list.Clear();
			foreach (KeyValuePair<int, ResourcesData> kv in mLoaded) {
				ResourcesData data = kv.Value;
				if (0 < data.refCount) { continue; }
				mResourcesToPath.Remove(GetResourcesKey(data.type, data.obj));
				async_loader.Release(data.obj);
				data.Reset();
				cached_resource_datas.Enqueue(data);
				to_remove_list.Add(kv.Key);
			}
			for (int i = 0, imax = to_remove_list.Count; i < imax; i++) {
				mLoaded.Remove(to_remove_list[i]);
			}
			to_remove_list.Clear();
			return mLoaded.Count <= 0;
		}

		private struct InstanceInfo {
			public Type type;
			public Object obj;
		}

		private class LoadingData {
			public int key;
			public Type type;
			private List<CallbackHolderBase> mCallbacks = new List<CallbackHolderBase>();
			private Action<Object> mOnLoaded;
			public LoadingData() { mOnLoaded = OnLoaded; }
			public void AddCallback(CallbackHolderBase callback) {
				if (callback == null) { return; }
				mCallbacks.Add(callback);
			}
			public void Clear() {
				for (int i = 0, imax = mCallbacks.Count; i < imax; i++) {
					CallbackHolderBase.Cache(mCallbacks[i]);
				}
				mCallbacks.Clear();
			}
			public Action<Object> onLoaded { get { return mOnLoaded; } }
			public Action<LoadingData, Object, int> onPreCallbacks;
			public Action<LoadingData, Object> onPostCallbacks;
			private void OnLoaded(Object obj) {
				int count = mCallbacks.Count;
				onPreCallbacks(this, obj, count);
				for (int i = 0; i < count; i++) {
					CallbackHolderBase callback = mCallbacks[i];
					callback.OnLoaded(obj);
					CallbackHolderBase.Cache(callback);
				}
				mCallbacks.Clear();
				onPostCallbacks(this, obj);
			}
		}

		private abstract class CallbackHolderBase {
			private Type mType;
			protected CallbackHolderBase(Type type) { mType = type; }
			public abstract void OnLoaded(Object obj);
			private static Dictionary<Type, Queue<CallbackHolderBase>> instances = new Dictionary<Type, Queue<CallbackHolderBase>>();
			public static CallbackHolder<T> Get<T>() where T : Object {
				Type type = typeof(T);
				CallbackHolder<T> ret = null;
				Queue<CallbackHolderBase> queue;
				if (instances.TryGetValue(type, out queue) && queue.Count > 0) {
					ret = queue.Dequeue() as CallbackHolder<T>;
				}
				return ret ?? new CallbackHolder<T>();
			}
			public static void Cache(CallbackHolderBase ins) {
				if (ins == null) { return; }
				Queue<CallbackHolderBase> queue;
				if (instances.TryGetValue(ins.mType, out queue)) {
					queue.Enqueue(ins);
				} else {
					queue = new Queue<CallbackHolderBase>();
					queue.Enqueue(ins);
					instances.Add(ins.mType, queue);
				}
			}
		}

		private class CallbackHolder<T> : CallbackHolderBase where T : Object {
			private OnResourcesLoadedDelegate<T> mCallback;
			public CallbackHolder() : base(typeof(T)) { }
			public CallbackHolder<T> SetCallback(OnResourcesLoadedDelegate<T> callback) { mCallback = callback; return this; }
			public override void OnLoaded(Object obj) {
				OnResourcesLoadedDelegate<T> callback = mCallback;
				mCallback = null;
				if (callback != null) {
					try { callback(obj as T); } catch (Exception e) { Debug.LogException(e); }
				}
			}
		}

	}
}