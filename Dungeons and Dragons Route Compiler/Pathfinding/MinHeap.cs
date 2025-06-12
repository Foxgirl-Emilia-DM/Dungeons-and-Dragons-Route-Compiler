// MinHeap.cs
using System;
using System.Collections.Generic;
using System.Linq; // Added for Any() in Contains

namespace YourFantasyWorldProject.Pathfinding
{
    // Represents an item in the priority queue
    public class PriorityQueueItem<T>
    {
        public T Value { get; set; }
        public double Priority { get; set; }

        public PriorityQueueItem(T value, double priority)
        {
            Value = value;
            Priority = priority;
        }
    }

    // A basic Min-Heap (Priority Queue) implementation
    // Used by Dijkstra's algorithm to efficiently get the unvisited node with the smallest distance
    public class MinHeap<T>
    {
        private List<PriorityQueueItem<T>> _elements;

        public int Count => _elements.Count;

        public MinHeap()
        {
            _elements = new List<PriorityQueueItem<T>>();
        }

        public void Enqueue(T item, double priority)
        {
            _elements.Add(new PriorityQueueItem<T>(item, priority));
            BubbleUp(_elements.Count - 1);
        }

        public T Dequeue()
        {
            if (_elements.Count == 0)
            {
                throw new InvalidOperationException("PriorityQueue is empty.");
            }

            // Get the root element (smallest priority)
            T result = _elements[0].Value;

            // Move the last element to the root
            int lastIndex = _elements.Count - 1;
            _elements[0] = _elements[lastIndex];
            _elements.RemoveAt(lastIndex); // Remove the last element

            // If there are still elements, sink down the new root
            if (_elements.Count > 0)
            {
                SinkDown(0);
            }

            return result;
        }

        public void UpdatePriority(T item, double newPriority)
        {
            for (int i = 0; i < _elements.Count; i++)
            {
                if (EqualityComparer<T>.Default.Equals(_elements[i].Value, item))
                {
                    if (newPriority < _elements[i].Priority)
                    {
                        _elements[i].Priority = newPriority;
                        BubbleUp(i);
                    }
                    // If new priority is greater, it might need to sink down, but for Dijkstra
                    // we only update if new priority is smaller.
                    // If it was possible to increase priority, we would call SinkDown(i) here.
                    break;
                }
            }
        }

        private void BubbleUp(int index)
        {
            while (index > 0)
            {
                int parentIndex = (index - 1) / 2;
                if (_elements[index].Priority < _elements[parentIndex].Priority)
                {
                    Swap(index, parentIndex);
                    index = parentIndex;
                }
                else
                {
                    break;
                }
            }
        }

        private void SinkDown(int index)
        {
            int leftChildIndex = 2 * index + 1;
            int rightChildIndex = 2 * index + 2;
            int smallestChildIndex = index;

            // Check if left child exists and has a smaller priority
            if (leftChildIndex < _elements.Count && _elements[leftChildIndex].Priority < _elements[smallestChildIndex].Priority)
            {
                smallestChildIndex = leftChildIndex;
            }

            // Check if right child exists and has a smaller priority than current smallest
            if (rightChildIndex < _elements.Count && _elements[rightChildIndex].Priority < _elements[smallestChildIndex].Priority)
            {
                smallestChildIndex = rightChildIndex;
            }

            // If the smallest child is not the current node, swap and continue sinking down
            if (smallestChildIndex != index)
            {
                Swap(index, smallestChildIndex);
                SinkDown(smallestChildIndex);
            }
        }

        private void Swap(int i, int j)
        {
            var temp = _elements[i];
            _elements[i] = _elements[j];
            _elements[j] = temp;
        }

        public bool Contains(T item)
        {
            return _elements.Any(e => EqualityComparer<T>.Default.Equals(e.Value, item));
        }
    }
}
