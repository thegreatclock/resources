namespace GreatClock.Common.ResourcesHolder {

	public static class HashString {

		public static int ComputeHash(string str) {
			if (str == null) { return -1; }
			lock (temp_string) {
				temp_string[0] = str;
				return ComputeHashInternal(temp_string, 1);
			}
		}

		public static int ComputeHash(string str1, string str2) {
			lock (temp_string) {
				int count = 0;
				if (str1 != null) {
					temp_string[count++] = str1;
				}
				if (str2 != null) {
					temp_string[count++] = str2;
				}
				return count <= 0 ? -1 : ComputeHashInternal(temp_string, count);
			}
		}

		public static string GetString(int hash) {
			if (hash < 0) { return null; }
			if (hash == 0) { return ""; }
			int h = (hash >> 20) - 1;
			if (h < 0 || h >= slots.Length) { return null; }
			int index = hash & 0xFFFFF;
			Slot s = slots[h];
			for (int i = 0; i < index; i++) {
				if (s == null) { return null; }
				s = s.next;
			}
			return s == null ? null : s.str;
		}

		private static string[] temp_string = new string[2];
		private static int[] indices = new int[] { 0, 1, 2, 3, 4 };
		private static Slot[] slots = new Slot[509];

		private static int ComputeHashInternal(string[] strs, int count) {
			int len = 0;
			for (int i = 0; i < count; i++) {
				len += strs[i].Length;
			}
			if (len <= 0) { return 0; }
			int ids = 5;
			if (len <= 5) {
				ids = len;
				for (int i = 1; i < len; i++) {
					indices[i] = i;
				}
			} else {
				int m = len >> 1;
				indices[4] = len - 1;
				indices[2] = m;
				indices[1] = m >> 1;
				indices[3] = (len + m) >> 1;
			}
			int h = len << 16;
			for (int i = 0; i < ids; i++) {
				int index = indices[i];
				for (int j = 0; j < count; j++) {
					string str = strs[j];
					int strLen = str.Length;
					if (index < strLen) {
						h ^= (int)str[index];
						break;
					}
					index -= strLen;
				}
			}
			h = h % slots.Length;
			int ret = (h + 1) << 20;
			Slot p = null;
			Slot s = slots[h];
			while (s != null) {
				if (len == s.str.Length) {
					bool equal = true;
					int index = 0;
					for (int i = 0; i < count; i++) {
						string str = strs[i];
						int strLen = str.Length;
						for (int j = 0; j < strLen; j++) {
							if (s.str[index++] != str[j]) {
								equal = false;
								break;
							}
						}
						if (!equal) { break; }
						/*if (string.CompareOrdinal(str, 0, s.str, index, strLen) != 0) {
							equal = false;
							break;
						}
						index += strLen;*/
					}
					if (equal) {
						break;
					}
				}
				p = s;
				s = s.next;
				ret++;
			}
			if (s == null) {
				s = new Slot();
				s.str = string.Join("", strs, 0, count);
				if (p == null) {
					slots[h] = s;
				} else {
					p.next = s;
				}
			}
			return ret;
		}

		private class Slot {
			public string str;
			public Slot next;
		}

	}

}
