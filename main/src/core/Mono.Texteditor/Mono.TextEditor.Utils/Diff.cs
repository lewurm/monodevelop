//
// Diff.cs
//
// Author:
//       Matthias Hertel, http://www.mathertel.de//
//       some tweaks made by Mike Krüger <mkrueger@novell.com>
//
// Copyright (c) by Matthias Hertel, http://www.mathertel.de//
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//
// diff.cs: A port of the algorythm to C#
// Copyright (c) by Matthias Hertel, http://www.mathertel.de
// This work is licensed under a BSD style license. See http://www.mathertel.de/License.aspx
//
// This Class implements the Difference Algorithm published in
// "An O(ND) Difference Algorithm and its Variations" by Eugene Myers
// Algorithmica Vol. 1 No. 2, 1986, p 251.
//
// There are many C, Java, Lisp implementations public available but they all seem to come
// from the same source (diffutils) that is under the (unfree) GNU public License
// and cannot be reused as a sourcecode for a commercial application.
// There are very old C implementations that use other (worse) algorithms.
// Microsoft also published sourcecode of a diff-tool (windiff) that uses some tree data.
// Also, a direct transfer from a C source to C# is not easy because there is a lot of pointer
// arithmetic in the typical C solutions and i need a managed solution.
// These are the reasons why I implemented the original published algorithm from the scratch and
// make it avaliable without the GNU license limitations.
// I do not need a high performance diff tool because it is used only sometimes.
// I will do some performace tweaking when needed.
//
// The algorithm itself is comparing 2 arrays of numbers so when comparing 2 text documents
// each line is converted into a (hash) number. See DiffText().
//
// Some chages to the original algorithm:
// The original algorithm was described using a recursive approach and comparing zero indexed arrays.
// Extracting sub-arrays and rejoining them is very performance and memory intensive so the same
// (readonly) data arrays are passed arround together with their lower and upper bounds.
// This circumstance makes the LCS and SMS functions more complicate.
// I added some code to the LCS function to get a fast response on sub-arrays that are identical,
// completely deleted or inserted.
//
// The result from a comparisation is stored in 2 arrays that flag for modified (deleted or inserted)
// lines in the 2 data arrays. These bits are then analysed to produce a array of Hunk objects.
//
// Further possible optimizations:
// (first rule: don't do it; second: don't do it yet)
// The arrays DataA and DataB are passed as parameters, but are never changed after the creation
// so they can be members of the class to avoid the paramter overhead.
// In SMS is a lot of boundary arithmetic in the for-D and for-k loops that can be done by increment
// and decrement of local variables.
// The DownVector and UpVector arrays are alywas created and destroyed each time the SMS gets called.
// It is possible to reuse tehm when transfering them to members of the class.
// See TODO: hints.
//
// Changes:
// 2002.09.20 There was a "hang" in some situations.
// Now I undestand a little bit more of the SMS algorithm.
// There have been overlapping boxes; that where analyzed partial differently.
// One return-point is enough.
// A assertion was added in CreateDiffs when in debug-mode, that counts the number of equal (no modified) lines in both arrays.
// They must be identical.
//
// 2003.02.07 Out of bounds error in the Up/Down vector arrays in some situations.
// The two vetors are now accessed using different offsets that are adjusted using the start k-Line.
// A test case is added.
//
// 2006.03.05 Some documentation and a direct Diff entry point.
//
// 2006.03.08 Refactored the API to static methods on the Diff class to make usage simpler.
// 2006.03.10 using the standard Debug class for self-test now.
//            compile with: csc /target:exe /out:diffTest.exe /d:DEBUG /d:TRACE /d:SELFTEST Diff.cs
// 2007.01.06 license agreement changed to a BSD style license.
// 2007.06.03 added the Optimize method.
// 2007.09.23 UpVector and DownVector optimization by Jan Stoklasa ().
// 2008.05.31 Adjusted the testing code that failed because of the Optimize method (not a bug in the diff algorithm).
// 2008.10.08 Fixing a test case and adding a new test case.

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Mono.TextEditor.Utils
{
	public struct Hunk
	{
		public static readonly Hunk Empty = new Hunk (-1, -1, 0, 0);

		public readonly int StartA;
		public readonly int StartB;

		public readonly int DeletedA;
		public readonly int InsertedB;

		public Hunk (int startA, int startB, int deletedA, int insertedB)
		{
			this.StartA = startA;
			this.StartB = startB;
			this.DeletedA = deletedA;
			this.InsertedB = insertedB;
		}
	}
	
	public sealed class Diff
	{
		/// <summary>
		/// Shortest Middle Snake Return Data
		/// </summary>
		struct SMSRD
		{
			internal int x, y;
		}

		static void Optimize<T> (DiffData<T> data)
		{
			int startPos = 0;
			while (startPos < data.Length) {
				while (startPos < data.Length && data.Modified[startPos] == false)
					startPos++;
				int endPos = startPos;
				while (endPos < data.Length && data.Modified[endPos] == true)
					endPos++;

				if (endPos < data.Length && data.Data[startPos].Equals (data.Data[endPos])) {
					data.Modified[startPos] = false;
					data.Modified[endPos] = true;
				} else {
					startPos = endPos;
				}
			}
		}

		public static IEnumerable<Hunk> CharDiff (string left, string right)
		{
			return GetDiff (left != null ? left.ToCharArray () : new char[0], right != null ? right.ToCharArray () : new char[0]);
		}

		internal static IEnumerable<Hunk> GetDiff<T> (T[] arrayA, T[] arrayB)
		{
			// The A-Version of the data (original data) to be compared.
			var dataA = new DiffData<T> (arrayA);

			// The B-Version of the data (modified data) to be compared.
			var dataB = new DiffData<T> (arrayB);

			int MAX = dataA.Length + dataB.Length + 1;
			/// vector for the (0,0) to (x,y) search
			int[] downVector = new int[2 * MAX + 2];
			/// vector for the (u,v) to (N,M) search
			int[] upVector = new int[2 * MAX + 2];

			LCS (dataA, 0, dataA.Length, dataB, 0, dataB.Length, downVector, upVector);
			return CreateDiffs (dataA, dataB);
		}
		
		/// <summary>Scan the tables of which lines are inserted and deleted,
		/// producing an edit script in forward order.
		/// </summary>
		/// dynamic array
		static IEnumerable<Hunk> CreateDiffs<T> (DiffData<T> dataA, DiffData<T> dataB)
		{
			int lineA = 0;
			int lineB = 0;
			while (lineA < dataA.Length || lineB < dataB.Length) {
				if (lineA < dataA.Length && !dataA.Modified[lineA] && lineB < dataB.Length && !dataB.Modified[lineB]) {
					// equal lines
					lineA++;
					lineB++;

				} else {
					// maybe deleted and/or inserted lines
					int startA = lineA;
					int startB = lineB;

					while (lineA < dataA.Length && (lineB >= dataB.Length || dataA.Modified[lineA]))
						// while (LineA < DataA.Length && DataA.Modified[LineA])
						lineA++;

					while (lineB < dataB.Length && (lineA >= dataA.Length || dataB.Modified[lineB]))
						// while (LineB < DataB.Length && DataB.Modified[LineB])
						lineB++;

					if (startA < lineA || startB < lineB) {
						// store a new difference-item
						yield return new Hunk (startA, startB, lineA - startA, lineB - startB);
					}
					// if
				}
				// if
			}
			// while
		}

		/// <summary>
		/// This is the algorithm to find the Shortest Middle Snake (SMS).
		/// </summary>
		/// <param name="dataA">sequence A</param>
		/// <param name="lowerA">lower bound of the actual range in DataA</param>
		/// <param name="upperA">upper bound of the actual range in DataA (exclusive)</param>
		/// <param name="dataB">sequence B</param>
		/// <param name="lowerB">lower bound of the actual range in DataB</param>
		/// <param name="upperB">upper bound of the actual range in DataB (exclusive)</param>
		/// <param name="downVector">a vector for the (0,0) to (x,y) search. Passed as a parameter for speed reasons.</param>
		/// <param name="upVector">a vector for the (u,v) to (N,M) search. Passed as a parameter for speed reasons.</param>
		/// <returns>a MiddleSnakeData record containing x,y and u,v</returns>
		static SMSRD SMS<T> (DiffData<T> dataA, int lowerA, int upperA, DiffData<T> dataB, int lowerB, int upperB, int[] downVector, int[] upVector)
		{
			SMSRD ret;
			int MAX = dataA.Length + dataB.Length + 1;

			int downK = lowerA - lowerB;
			// the k-line to start the forward search
			int upK = upperA - upperB;
			// the k-line to start the reverse search
			int delta = (upperA - lowerA) - (upperB - lowerB);
			bool oddDelta = (delta & 1) != 0;

			// The vectors in the publication accepts negative indexes. the vectors implemented here are 0-based
			// and are access using a specific offset: UpOffset UpVector and DownOffset for DownVektor
			int downOffset = MAX - downK;
			int upOffset = MAX - upK;

			int MaxD = ((upperA - lowerA + upperB - lowerB) / 2) + 1;

			// Debug.Write(2, "SMS", String.Format("Search the box: A[{0}-{1}] to B[{2}-{3}]", LowerA, UpperA, LowerB, UpperB));

			// init vectors
			downVector[downOffset + downK + 1] = lowerA;
			upVector[upOffset + upK - 1] = upperA;

			for (int D = 0; D <= MaxD; D++) {

				// Extend the forward path.
				for (int k = downK - D; k <= downK + D; k += 2) {
					// Debug.Write(0, "SMS", "extend forward path " + k.ToString());

					// find the only or better starting point
					int x, y;
					if (k == downK - D) {
						x = downVector[downOffset + k + 1];
						// down
					} else {
						x = downVector[downOffset + k - 1] + 1;
						// a step to the right
						if (k < downK + D && downVector[downOffset + k + 1] >= x)
							x = downVector[downOffset + k + 1];
						// down
					}
					y = x - k;

					// find the end of the furthest reaching forward D-path in diagonal k.
					while (x < upperA && y < upperB && dataA.Data[x].Equals (dataB.Data[y])) {
						x++;
						y++;
					}
					downVector[downOffset + k] = x;

					// overlap ?
					if (oddDelta && upK - D < k && k < upK + D) {
						if (upVector[upOffset + k] <= downVector[downOffset + k]) {
							ret.x = downVector[downOffset + k];
							ret.y = downVector[downOffset + k] - k;
							// ret.u = UpVector[UpOffset + k];      // 2002.09.20: no need for 2 points
							// ret.v = UpVector[UpOffset + k] - k;
							return (ret);
						}
						// if
					}
					// if
				}
				// for k
				// Extend the reverse path.
				for (int k = upK - D; k <= upK + D; k += 2) {
					// Debug.Write(0, "SMS", "extend reverse path " + k.ToString());

					// find the only or better starting point
					int x, y;
					if (k == upK + D) {
						x = upVector[upOffset + k - 1];
						// up
					} else {
						x = upVector[upOffset + k + 1] - 1;
						// left
						if (k > upK - D && upVector[upOffset + k - 1] < x)
							x = upVector[upOffset + k - 1];
						// up
					}
					// if
					y = x - k;

					while (x > lowerA && y > lowerB && dataA.Data[x - 1].Equals (dataB.Data[y - 1])) {
						x--;
						y--;
						// diagonal
					}
					upVector[upOffset + k] = x;

					// overlap ?
					if (!oddDelta && downK - D <= k && k <= downK + D) {
						if (upVector[upOffset + k] <= downVector[downOffset + k]) {
							ret.x = downVector[downOffset + k];
							ret.y = downVector[downOffset + k] - k;
							// ret.u = UpVector[UpOffset + k];     // 2002.09.20: no need for 2 points
							// ret.v = UpVector[UpOffset + k] - k;
							return (ret);
						}
						// if
					}
					// if
				}
				// for k
			}
			// for D
			throw new ApplicationException ("the algorithm should never come here.");
		}
		// SMS

		/// <summary>
		/// This is the divide-and-conquer implementation of the longes common-subsequence (LCS)
		/// algorithm.
		/// The published algorithm passes recursively parts of the A and B sequences.
		/// To avoid copying these arrays the lower and upper bounds are passed while the sequences stay constant.
		/// </summary>
		/// <param name="dataA">sequence A</param>
		/// <param name="lowerA">lower bound of the actual range in DataA</param>
		/// <param name="upperA">upper bound of the actual range in DataA (exclusive)</param>
		/// <param name="dataB">sequence B</param>
		/// <param name="lowerB">lower bound of the actual range in DataB</param>
		/// <param name="upperB">upper bound of the actual range in DataB (exclusive)</param>
		/// <param name="downVector">a vector for the (0,0) to (x,y) search. Passed as a parameter for speed reasons.</param>
		/// <param name="upVector">a vector for the (u,v) to (N,M) search. Passed as a parameter for speed reasons.</param>
		static void LCS<T> (DiffData<T> dataA, int lowerA, int upperA, DiffData<T> dataB, int lowerB, int upperB, int[] downVector, int[] upVector)
		{
			// Fast walkthrough equal lines at the start
			while (lowerA < upperA && lowerB < upperB && dataA.Data[lowerA].Equals (dataB.Data[lowerB])) {
				lowerA++;
				lowerB++;
			}

			// Fast walkthrough equal lines at the end
			while (lowerA < upperA && lowerB < upperB && dataA.Data[upperA - 1].Equals (dataB.Data[upperB - 1])) {
				--upperA;
				--upperB;
			}

			if (lowerA == upperA) {
				// mark as inserted lines.
				while (lowerB < upperB)
					dataB.Modified[lowerB++] = true;

			} else if (lowerB == upperB) {
				// mark as deleted lines.
				while (lowerA < upperA)
					dataA.Modified[lowerA++] = true;

			} else {
				// Find the middle snakea and length of an optimal path for A and B
				SMSRD smsrd = SMS (dataA, lowerA, upperA, dataB, lowerB, upperB, downVector, upVector);
				// Debug.Write(2, "MiddleSnakeData", String.Format("{0},{1}", smsrd.x, smsrd.y));

				// The path is from LowerX to (x,y) and (x,y) to UpperX
				LCS (dataA, lowerA, smsrd.x, dataB, lowerB, smsrd.y, downVector, upVector);
				LCS (dataA, smsrd.x, upperA, dataB, smsrd.y, upperB, downVector, upVector);
				// 2002.09.20: no need for 2 points
			}
		}
		// LCS()

		
	}
	
	/// <summary>Data on one input file being compared.
	/// </summary>
	class DiffData<T>
	{
		/// <summary>Number of elements (lines).</summary>
		public readonly int Length;

		/// <summary>Buffer of numbers that will be compared.</summary>
		public readonly T[] Data;

		/// <summary>
		/// Array of booleans that flag for modified data.
		/// This is the result of the diff.
		/// This means deletedA in the first Data or inserted in the second Data.
		/// </summary>
		public readonly bool[] Modified;

		/// <summary>
		/// Initialize the Diff-Data buffer.
		/// </summary>
		/// <param name="data">reference to the buffer</param>
		public DiffData (T[] initData)
		{
			Data = initData;
			Length = initData.Length;
			Modified = new bool[Length + 2];
		}
		// DiffData
	}
	// class DiffData
}
// namespace
