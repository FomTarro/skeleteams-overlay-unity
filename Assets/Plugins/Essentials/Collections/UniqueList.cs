using System.Collections;
using System.Collections.Generic;

namespace Skeletom.Essentials.Collections
{
	public class UniqueList<T> : IList<T>
	{
		private List<T> _innerList = new List<T>();
		public T this[int index] { get => this._innerList[index]; set => this._innerList[index] = value; }

		public int Count => this._innerList.Count;

		public bool IsReadOnly => false;

		public T GetLastItem()
		{
			return this._innerList.Count > 0 ? this._innerList[this._innerList.Count - 1] : default(T);
		}

		public void Add(T item)
		{
			if (this._innerList.Contains(item))
			{
				this._innerList.Remove(item);
			}
			this._innerList.Add(item);
		}

		public void Clear()
		{
			this._innerList.Clear();
		}

		public bool Contains(T item)
		{
			return this._innerList.Contains(item);
		}

		public void CopyTo(T[] array, int arrayIndex)
		{
			this._innerList.CopyTo(array, arrayIndex);
		}

		public IEnumerator<T> GetEnumerator()
		{
			return this._innerList.GetEnumerator();
		}

		public int IndexOf(T item)
		{
			return this._innerList.IndexOf(item);
		}

		public void Insert(int index, T item)
		{
			if (this._innerList.Contains(item))
			{
				throw new System.Exception("Value already exists in list.");
			}
			else
			{
				this._innerList.Insert(index, item);
			}
		}

		public bool Remove(T item)
		{
			return this._innerList.Remove(item);
		}

		public void RemoveAt(int index)
		{
			this._innerList.RemoveAt(index);
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return this._innerList.GetEnumerator();
		}
	}
}
