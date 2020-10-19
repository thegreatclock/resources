using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GreatClock.Common.ResourcesHolder {

	public partial class ResourcesHolder {

		private Dictionary<int, ResourcesData> mLoaded = new Dictionary<int, ResourcesData>();

		private Dictionary<int, List<InstanceInfo>> mUsingInstances = new Dictionary<int, List<InstanceInfo>>();

		private Dictionary<int, List<InstanceInfo>> mCachedInstances = new Dictionary<int, List<InstanceInfo>>();

		private static Queue<List<InstanceInfo>> cached_instance_list = new Queue<List<InstanceInfo>>();

		public void GetInstance<T>(string path, OnResourcesLoadedDelegate<T> callback) where T : Object {
			GetInstanceInternal(null, path, typeof(T), true, false, Vector3.zero, Quaternion.identity, callback);
		}

		public void GetInstance<T>(string folder, string file, OnResourcesLoadedDelegate<T> callback) where T : Object {
			GetInstanceInternal(folder, file, typeof(T), true, false, Vector3.zero, Quaternion.identity, callback);
		}

		public void GetInstance<T>(string path, bool actived, OnResourcesLoadedDelegate<T> callback) where T : Object {
			GetInstanceInternal(null, path, typeof(T), actived, false, Vector3.zero, Quaternion.identity, callback);
		}

		public void GetInstance<T>(string folder, string file, bool actived, OnResourcesLoadedDelegate<T> callback) where T : Object {
			GetInstanceInternal(folder, file, typeof(T), actived, false, Vector3.zero, Quaternion.identity, callback);
		}

		public void GetInstance<T>(string path, Vector3 position, Quaternion rotation, OnResourcesLoadedDelegate<T> callback) where T : Object {
			GetInstanceInternal(null, path, typeof(T), true, true, position, rotation, callback);
		}

		public void GetInstance<T>(string folder, string file, Vector3 position, Quaternion rotation, OnResourcesLoadedDelegate<T> callback) where T : Object {
			GetInstanceInternal(folder, file, typeof(T), true, true, position, rotation, callback);
		}

		public void GetInstance<T>(string path, bool actived, Vector3 position, Quaternion rotation, OnResourcesLoadedDelegate<T> callback) where T : Object {
			GetInstanceInternal(null, path, typeof(T), actived, true, position, rotation, callback);
		}

		public void GetInstance<T>(string folder, string file, bool actived, Vector3 position, Quaternion rotation, OnResourcesLoadedDelegate<T> callback) where T : Object {
			GetInstanceInternal(folder, file, typeof(T), actived, true, position, rotation, callback);
		}

		public T GetInstanceSync<T>(string path) where T : Object {
			return GetInstanceSyncInternal<T>(null, path, true, false, Vector3.zero, Quaternion.identity);
		}

		public T GetInstanceSync<T>(string folder, string file) where T : Object {
			return GetInstanceSyncInternal<T>(folder, file, true, false, Vector3.zero, Quaternion.identity);
		}

		public T GetInstanceSync<T>(string path, bool actived) where T : Object {
			return GetInstanceSyncInternal<T>(null, path, actived, false, Vector3.zero, Quaternion.identity);
		}

		public T GetInstanceSync<T>(string folder, string file, bool actived) where T : Object {
			return GetInstanceSyncInternal<T>(folder, file, actived, false, Vector3.zero, Quaternion.identity);
		}

		public T GetInstanceSync<T>(string path, Vector3 position, Quaternion rotation) where T : Object {
			return GetInstanceSyncInternal<T>(null, path, true, true, position, rotation);
		}

		public T GetInstanceSync<T>(string folder, string file, Vector3 position, Quaternion rotation) where T : Object {
			return GetInstanceSyncInternal<T>(folder, file, true, true, position, rotation);
		}

		public T GetInstanceSync<T>(string path, bool actived, Vector3 position, Quaternion rotation) where T : Object {
			return GetInstanceSyncInternal<T>(null, path, actived, true, position, rotation);
		}

		public T GetInstanceSync<T>(string folder, string file, bool actived, Vector3 position, Quaternion rotation) where T : Object {
			return GetInstanceSyncInternal<T>(folder, file, actived, true, position, rotation);
		}

		public bool ReleaseInstance<T>(T obj) where T : Object {
			return ReleaseInstanceInternal(typeof(T), obj);
		}

		public void GetInstance(string path, Type type, OnResourcesLoadedDelegate<Object> callback) {
			GetInstanceInternal(null, path, type, true, false, Vector3.zero, Quaternion.identity, callback);
		}

		public void GetInstance(string path, Type type, bool actived, OnResourcesLoadedDelegate<Object> callback) {
			GetInstanceInternal(null, path, type, actived, false, Vector3.zero, Quaternion.identity, callback);
		}

		public void GetInstance(string path, Type type, Vector3 position, Quaternion rotation, OnResourcesLoadedDelegate<Object> callback) {
			GetInstanceInternal(null, path, type, true, true, position, rotation, callback);
		}

		public void GetInstance(string path, Type type, bool actived, Vector3 position, Quaternion rotation, OnResourcesLoadedDelegate<Object> callback) {
			GetInstanceInternal(null, path, type, actived, true, position, rotation, callback);
		}

		public bool ReleaseInstance(Type type, Object obj) {
			return ReleaseInstanceInternal(type, obj);
		}

		private List<InstanceInfo> GetUsingInstanceList(int key) {
			List<InstanceInfo> ret;
			if (mUsingInstances.TryGetValue(key, out ret)) {
				return ret;
			}
			ret = 0 < cached_instance_list.Count ? cached_instance_list.Dequeue() : new List<InstanceInfo>(8);
			mUsingInstances.Add(key, ret);
			ret.Clear();
			return ret;
		}

		private T GetCachedInstance<T>(int key) where T : Object {
			List<InstanceInfo> list;
			if (!mCachedInstances.TryGetValue(key, out list)) {
				return null;
			}
			T ret = null;
			while (0 < list.Count) {
				InstanceInfo instance = list[0];
				list.RemoveAt(0);
				if (instance.obj != null && !instance.obj.Equals(null)) {
					List<InstanceInfo> usingList = GetUsingInstanceList(key);
					usingList.Add(instance);
					ret = instance.obj as T;
					break;
				}
				Debug.LogErrorFormat("[ResourcesHolder] GetCachedInstance() Object instance of '{0}' is destroyed unexpectedly",
					HashString.GetString(key));
			}
			if (list.Count <= 0) {
				mCachedInstances.Remove(key);
				cached_instance_list.Enqueue(list);
			}
			return ret;
		}

		private static int sCacheCountFrame;
		private static int sLastFrame;
		private static int sTargetFrame;

		private void GetInstanceInternal<T>(string folder, string file, Type type, bool actived, bool initTrans, Vector3 position, Quaternion rotation, OnResourcesLoadedDelegate<T> callback) where T : Object {
			if (string.IsNullOrEmpty(file)) { return; }
			int key = HashString.ComputeHash(folder, file);
			T obj = GetCachedInstance<T>(key);
			if (obj != null) {
				GameObject go = obj as GameObject;
				if (go != null) {
					go.SetActive(false);
					Transform t = go.transform;
					t.SetParent(null, true);
					if (initTrans) {
						t.position = position;
						t.rotation = rotation;
					}
					if (actived) { go.SetActive(true); }
				}
				try { callback(obj); } catch (Exception e) { Debug.LogException(e); }
				return;
			}
			InstanceLoadingData<T> data = GetInstanceLoadingData<T>(key, type, actived, initTrans, position, rotation, callback);
			GetResourcesInternal<T>(folder, file, type, data.onLoaded);
		}

		private T GetInstanceSyncInternal<T>(string folder, string file, bool actived, bool initTrans, Vector3 position, Quaternion rotation) where T : Object {
			if (string.IsNullOrEmpty(file)) { return null; }
			int key = HashString.ComputeHash(folder, file);
			T obj = GetCachedInstance<T>(key);
			if (obj == null) {
				Type type = typeof(T);
				T prefab = GetResourcesInternalSync<T>(folder, file, type);
				if (prefab == null) { return null; }
				obj = initTrans && prefab is GameObject ?
					Object.Instantiate(prefab, position, rotation) : Object.Instantiate(prefab);
				OnInstanceCreated(key, type, obj);
			}
			GameObject go = obj as GameObject;
			if (go != null) {
				go.SetActive(false);
				Transform t = go.transform;
				t.SetParent(null, true);
				if (initTrans) {
					t.position = position;
					t.rotation = rotation;
				}
				if (actived) { go.SetActive(true); }
			}
			return obj;
		}

		private bool ReleaseInstanceInternal(Type type, Object obj) {
			if (obj == null) {
				return false;
			}
			int key;
			if (!mResourcesToPath.TryGetValue(GetResourcesKey(type, obj), out key)) {
				return false;
			}
			List<InstanceInfo> list;
			if (!mUsingInstances.TryGetValue(key, out list)) {
				return false;
			}
			InstanceInfo findObj = FindAndRemoveObj(list, obj);
			if (findObj.obj == null) {
				return false;
			}
			List<InstanceInfo> cachedInstanceList = GetCachedInstanceList(key);
			cachedInstanceList.Add(findObj);

			GameObject go = obj as GameObject;
			if (go != null) {
				go.transform.SetParent(GetCachedGameObjectRoot());
			}
			if (list.Count <= 0) {
				mUsingInstances.Remove(key);
				cached_instance_list.Enqueue(list);
			}

			return true;
		}

		private Action<int, Type, Object> mOnInstanceCreated;
		private void OnInstanceCreated(int key, Type type, Object instance) {
			mResourcesToPath.Add(GetResourcesKey(type, instance), key);
			List<InstanceInfo> useList = GetUsingInstanceList(key);
			useList.Add(new InstanceInfo { type = type, obj = instance });
		}

		private List<InstanceInfo> GetCachedInstanceList(int key) {
			List<InstanceInfo> list;
			if (!mCachedInstances.TryGetValue(key, out list)) {
				list = cached_instance_list.Count > 0 ? cached_instance_list.Dequeue() : new List<InstanceInfo>(8);
				mCachedInstances.Add(key, list);
			}
			return list;
		}

		private bool ReleaseUnusedInstancesInternal() {
			foreach (KeyValuePair<int, List<InstanceInfo>> kv in mCachedInstances) {
				ResourcesData data;
				if (!mLoaded.TryGetValue(kv.Key, out data)) {
					continue;
				}
				List<InstanceInfo> infoList = kv.Value;
				for (int i = 0, imax = infoList.Count; i < imax; i++) {
					data.refCount--;
					InstanceInfo to = infoList[i];
					mResourcesToPath.Remove(GetResourcesKey(to.type, to.obj));
					Object.Destroy(to.obj);
				}
				infoList.Clear();
				cached_instance_list.Enqueue(infoList);
			}
			mCachedInstances.Clear();
			return mUsingInstances.Count <= 0;
		}

		private long GetResourcesKey(Type type, Object instance) {
			long t = type.GetHashCode();
			long i = instance.GetInstanceID();
			return (t << 32) | i;
		}

		private InstanceInfo FindAndRemoveObj(List<InstanceInfo> list, Object obj) {
			for (int i = 0, imax = list.Count; i < imax; i++) {
				InstanceInfo to = list[i];
				if (obj == to.obj) {
					list.RemoveAt(i);
					return to;
				}
			}
			return new InstanceInfo();
		}

		private Dictionary<Type, Queue<InstanceLoadingDataBase>> mCachedInstanceLoadingDatas = new Dictionary<Type, Queue<InstanceLoadingDataBase>>();
		private InstanceLoadingData<T> GetInstanceLoadingData<T>(int key, Type type, bool actived, bool initTrans,
			Vector3 position, Quaternion rotation, OnResourcesLoadedDelegate<T> callback) where T : Object {
			InstanceLoadingData<T> ret = null;
			Type t = typeof(T);
			Queue<InstanceLoadingDataBase> queue;
			if (mCachedInstanceLoadingDatas.TryGetValue(t, out queue) && queue.Count > 0) {
				ret = queue.Dequeue() as InstanceLoadingData<T>;
			}
			if (ret == null) {
				ret = new InstanceLoadingData<T>();
				if (mOnInstanceCreated == null) {
					mOnInstanceCreated = OnInstanceCreated;
				}
				ret.onInstanceCreated = mOnInstanceCreated;
				if (mCacheInstanceLoadingData == null) {
					mCacheInstanceLoadingData = CacheInstanceLoadingData;
				}
				ret.onFinished = mCacheInstanceLoadingData;
			}
			ret.key = key;
			ret.type = type;
			ret.actived = actived;
			ret.initTrans = initTrans;
			ret.position = position;
			ret.rotation = rotation;
			ret.callback = callback;
			return ret;
		}
		private Action<InstanceLoadingDataBase> mCacheInstanceLoadingData;
		private void CacheInstanceLoadingData(InstanceLoadingDataBase data) {
			if (data == null) { return; }
			data.Clear();
			Queue<InstanceLoadingDataBase> queue;
			if (mCachedInstanceLoadingDatas.TryGetValue(data.tType, out queue)) {
				queue.Enqueue(data);
			} else {
				queue = new Queue<InstanceLoadingDataBase>();
				queue.Enqueue(data);
				mCachedInstanceLoadingDatas.Add(data.tType, queue);
			}
		}

		private abstract class InstanceLoadingDataBase {
			public readonly Type tType;
			protected InstanceLoadingDataBase(Type type) { this.tType = type; }
			public abstract void Clear();
		}

		private class InstanceLoadingData<T> : InstanceLoadingDataBase where T : Object {
			//parameters
			public int key;
			public Type type;
			public bool actived;
			public bool initTrans;
			public Vector3 position;
			public Quaternion rotation;
			public OnResourcesLoadedDelegate<T> callback;
			//callbacks
			private OnResourcesLoadedDelegate<T> mOnLoaded;
			public Action<int, Type, Object> onInstanceCreated;
			public Action<InstanceLoadingDataBase> onFinished;
			//others
			public OnResourcesLoadedDelegate<T> onLoaded { get { return mOnLoaded; } }

			public InstanceLoadingData() : base(typeof(T)) {
				mOnLoaded = OnResourcesLoaded;
			}

			public override void Clear() {
				callback = null;
			}

			private void OnResourcesLoaded(T obj) {
				T instance = null;
				if (obj != null) {
					instance = initTrans && obj is GameObject ?
						Object.Instantiate(obj, position, rotation) : Object.Instantiate(obj);
					GameObject retGo = instance as GameObject;
					if (retGo != null) {
						retGo.SetActive(actived);
					}
					if (onInstanceCreated != null) {
						onInstanceCreated(key, type, instance);
					}
				}
				try { callback(instance); } catch (Exception e) { Debug.LogException(e); }
				onFinished(this);
			}
		}

	}
}