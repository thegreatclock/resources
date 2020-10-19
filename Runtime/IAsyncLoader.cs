using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GreatClock.Common.ResourcesHolder {

	public interface IAsyncLoader {

		void Load(string path, Type type, Action<Object> callback);

		void Release(Object obj);

	}

}
