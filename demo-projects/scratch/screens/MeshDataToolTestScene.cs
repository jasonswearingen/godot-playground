using Godot;
using System;
using System.Collections.Generic;




[Tool]
public class MeshDataToolTestScene : Spatial
{
	// Declare member variables here. Examples:
	// private int a = 2;
	// private string b = "text";

	// Called when the node enters the scene tree for the first time.


	TimeSpan timer;

	//ArrayMesh mesh;
	public override void _Ready()
	{
		GD.Print("REBUILDING COLOR MESH");

		var inputNode = FindNode("Input") as MeshInstance;
		var outputNode = FindNode("Output") as MeshInstance;
		//var mesh = GD.Load<ArrayMesh>("res://asset/fish/Fish1.obj");
		var mesh = inputNode.Mesh as ArrayMesh;

		if (mesh == null)
		{
			return;
		}


		//var mdt = new MeshDataTool();
		//mdt.CreateFromSurface(mesh, 0);
		//_SetVertexColorToBarycentric(mdt);
		//mesh.SurfaceRemove(0);
		//mdt.CommitToSurface(mesh);
		//mdt.Dispose();


		var surfaceCount = mesh.GetSurfaceCount();
		var surfaceQueue = new Queue<MeshDataTool>();
		for (var i = 0; i < surfaceCount; i++)
		{
			var mdt = new MeshDataTool();
			mdt.CreateFromSurface(mesh, 0);

			BarycentricProcessor._SetVertexColorToBarycentric(mdt);
			mesh.SurfaceRemove(0);
			surfaceQueue.Enqueue(mdt);
		}

		//replace our mesh with modified version
		while (surfaceQueue.Count > 0)
		{
			var mdt = surfaceQueue.Dequeue();
			mdt.CommitToSurface(mesh);
			mdt.Dispose();
		}

		outputNode.Mesh = mesh;

		//GD.Print($"mark fails= {markFailCount}");
	}

	//  // Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(float delta)
	{
		//timer.Add(TimeSpan.FromSeconds(delta));

		//if (Engine.EditorHint)
		//{
		//	if (timer.TotalSeconds > 5)
		//	{
		//		timer = TimeSpan.Zero;
		//		_Ready();
		//	}
		//}
	}


	public override void _Input(InputEvent @event)
	{


		base._Input(@event);
		if (Input.IsKeyPressed((int)KeyList.End))
		{
			GD.Print("ui_end pressed.   reloading model");
			_Ready();
		}
	}
}

public class BarycentricProcessor
{

	[Flags]
	public enum VERTCOLOR
	{
		NONE = 0,
		Red = 1,
		Green = 2,
		Blue = 4,
	}

	public class VertexInfo : IComparable<VertexInfo>
	{
		public int vertIdx;

		public Vector3 pos;

		/// <summary>
		/// edges associated with
		/// </summary>
		public int[] edges;

		public int[] faces;

		public int[] adjacentVerticies;

		public VertexInfo[] storage;

		//public int adjadj;

		public MeshDataTool mdt;
		public VertexInfo(int idx, MeshDataTool mdt, VertexInfo[] storage)
		{
			this.storage = storage;
			this.vertIdx = idx;
			this.mdt = mdt;
			edges = mdt.GetVertexEdges(idx);
			faces = mdt.GetVertexFaces(idx);
			this.pos = mdt.GetVertex(idx);

			adjacentVerticies = new int[edges.Length];
			for (var i = 0; i < edges.Length; i++)
			{
				var edgeIdx = edges[i];
				var edgeVert0 = mdt.GetEdgeVertex(edgeIdx, 0);
				var edgeVert1 = mdt.GetEdgeVertex(edgeIdx, 1);
				var adjacent = edgeVert0 != idx ? edgeVert0 : edgeVert1;
				adjacentVerticies[i] = adjacent;
			}

			////better heuristic for sorting (vert with most adjcent faces)
			//foreach (var faceIdx in faces)
			//{
			//	//adjadj++;
			//	for (var i = 0; i < 3; i++)
			//	{
			//		var faceVertIdx = mdt.GetFaceVertex(faceIdx, i);
			//		adjadj += mdt.GetVertexFaces(faceVertIdx).Length;
			//	}

			//}
		}

		public IEnumerable<VertexInfo> GetAdjacentVertInfo()
		{
			for (var i = 0; i < adjacentVerticies.Length; i++)
			{
				yield return storage[adjacentVerticies[i]];
			}
		}

		public override string ToString()
		{
			return $"idx={vertIdx} order={adjacentVerticies.Length}";
		}

		public int CompareTo(VertexInfo other)
		{
			//lowest first
			return (adjacentVerticies.Length - other.adjacentVerticies.Length);// * 100000 + (adjadj - other.adjadj);
		}

		public VERTCOLOR GetUsedAdjacentColors()
		{
			//use a flags enumeration to store used colors
			var usedColors = VERTCOLOR.NONE;
			foreach (var adjIdx in adjacentVerticies)
			{
				var adjColor = GetVertColor(adjIdx, mdt);
				if ((usedColors & adjColor) != 0)
				{
					throw new Exception($"color {adjColor} already found in adjacents: {usedColors}");
				}
				usedColors |= adjColor;
			}

			return usedColors;
		}

		public void SetAvailableColor()
		{
			//get colors used by adjacents
			var usedAdjColors = GetUsedAdjacentColors();

			//get available color
			VERTCOLOR colorToUse = VERTCOLOR.NONE;
			foreach (VERTCOLOR maybeColor in Enum.GetValues(typeof(VERTCOLOR)))
			{
				switch (maybeColor)
				{
					case VERTCOLOR.NONE:
						break;
					default:
						if ((usedAdjColors & maybeColor) == 0)
						{
							//color is free
							colorToUse = maybeColor;
						}
						break;
				}
				if (colorToUse != VERTCOLOR.NONE)
				{
					break;
				}
			}
			SetVertColor(vertIdx, mdt, colorToUse);


		}

		/// <summary>
		/// will set the color if our vertex color is black (IE: not already set), and no adjacent verticies are using that color
		/// if color already set to the same color, will also return true
		/// </summary>
		/// <param name="c"></param>
		/// <returns></returns>
		public bool TrySetAvailableColor(Color c, bool force = false)
		{
			if (force == true)
			{
				mdt.SetVertexColor(vertIdx, c);
				return true;
			}
			var currentColor = mdt.GetVertexColor(vertIdx);
			if (currentColor == c)
			{
				return true;
			}
			//
			if (currentColor != Colors.Transparent)
			{
				return false;
			}
			foreach (var adjIdx in adjacentVerticies)
			{
				var adjColor = mdt.GetVertexColor(adjIdx);
				if (adjColor == c)
				{
					return false;
				}
			}
			mdt.SetVertexColor(vertIdx, c);
			return true;
		}



		static VERTCOLOR GetVertColor(int vertIdx, MeshDataTool mdt)
		{
			var color = mdt.GetVertexColor(vertIdx);
			switch (color)
			{
				case Color c when c == Colors.Red:
					return VERTCOLOR.Red;

				case Color c when c == Colors.Green:
					return VERTCOLOR.Green;

				case Color c when c == Colors.Blue:
					return VERTCOLOR.Blue;
				default:
					return VERTCOLOR.NONE;

			}
		}
		static void SetVertColor(int vertIdx, MeshDataTool mdt, VERTCOLOR color)
		{
			Color c;
			switch (color)
			{
				case VERTCOLOR.Red:
					c = Colors.Red;
					break;
				case VERTCOLOR.Green:
					c = Colors.Green;
					break;
				case VERTCOLOR.Blue:
					c = Colors.Blue;
					break;
				//case VERTCOLOR.NONE:
				//	c = Colors.Black;
				//	break;
				default:
					throw new Exception($"unexpected setting vert color {color}  vertIdx={vertIdx}.  are you passing a single value or a flags union?");

			}
			mdt.SetVertexColor(vertIdx, c);
		}

	}

	/// <summary>
	/// 4th attempt:  set color by most linked, not by color
	/// </summary>
	/// <param name="mdt"></param>
	public static void _SetVertexColorToBarycentric(MeshDataTool mdt)
	{
		//store info about our verticies into an array
		var vertStorage = new VertexInfo[mdt.GetVertexCount()];
		for (var vertIdx = 0; vertIdx < vertStorage.Length; vertIdx++)
		{
			vertStorage[vertIdx] = new VertexInfo(vertIdx, mdt, vertStorage);
			//set vert color to black
			mdt.SetVertexColor(vertIdx, Colors.Transparent);
		}

		//sort verticies by degree (number of edges).   defaults to highest first
		var sortedVerts = new List<VertexInfo>(vertStorage);
		sortedVerts.Sort();
		//verts.CopyTo(sortedVerts,0);
		//Array.Sort(sortedVerts);

		var colorChoices = new Color[] { new Color(1, 0, 0, 0), new Color(0, 1, 0, 0), new Color(0, 0, 1, 0), new Color(0, 0, 0, 1) };

		//_GREEDY_FACE(sortedVerts, colorChoices, mdt);
		//_GREEDY_BASIC(sortedVerts, colorChoices, mdt);
		//_CYBERREALITY(sortedVerts, colorChoices, mdt);
		_CYBERREALITY_EDIT(sortedVerts, colorChoices, mdt);
		//_WELSH_POWELL_ADJUSTED(sortedVerts, colorChoices, mdt);

	}



	private static void _CYBERREALITY_EDIT(List<VertexInfo> sortedVerts, Color[] _colorChoices, MeshDataTool mdt)
	{
		var done = new Dictionary<int, bool>();//vertidx/isDone
		var vertColorStorage = new Dictionary<int, Color>(); //vertidx/color
		var rand = new Random(0);
		var rand_color = new Color((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble());
		var faceVert0MetaInfo = new Dictionary<int, (Vector3 normal, Color color)>(); //normalIdx/normal
		for (var faceIdx = 0; faceIdx < mdt.GetFaceCount(); faceIdx++)
		{
			var faceVert0Idx = mdt.GetFaceVertex(faceIdx, 0);
			var faceVert0Norm = mdt.GetVertexNormal(faceVert0Idx);
			var colorChoices = new List<Color>() { Colors.Red, Colors.Green, Colors.Blue };
			//////JASON CLEANUP:  this code block isn't actually used in the algo...
			////foreach (var n in faceVert0MetaInfo.Keys)
			////{
			////	var dot = faceVert0Norm.Dot(faceVert0MetaInfo[n].normal);
			////	if (dot == 1)
			////	{
			////		rand_color = faceVert0MetaInfo[n].color;
			////	}
			////	else
			////	{
			////		rand_color = new Color((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble());
			////	}
			////}
			////faceVert0MetaInfo[faceVert0Idx] = (normal: faceVert0Norm, color: rand_color);
			

			//loop through all verts for the face, and remove colors from our colorChoices if a vert is already using it
			for (var faceVertId = 0; faceVertId < 3; faceVertId++)
			{
				var vertIdx = mdt.GetFaceVertex(faceIdx, faceVertId);
				if (vertColorStorage.ContainsKey(vertIdx))
				{
					colorChoices.Remove(vertColorStorage[vertIdx]);
				}
			}


			for (var faceVertId = 0; faceVertId < 3; faceVertId++)
			{
				var vertIdx = mdt.GetFaceVertex(faceIdx, faceVertId);
				if (!done.ContainsKey(vertIdx) || done[vertIdx] != true)
				{
					done[vertIdx] = true;
					var removal = Colors.Black;

					var vert_0 = mdt.GetFaceVertex(faceIdx, 0);
					var vert_1 = mdt.GetFaceVertex(faceIdx, 1);
					var vert_2 = mdt.GetFaceVertex(faceIdx, 2);
					var edge_a = mdt.GetVertex(vert_2).DirectionTo(mdt.GetVertex(vert_0));
					var edge_b = mdt.GetVertex(vert_2).DirectionTo(mdt.GetVertex(vert_1));
					var edge_c = mdt.GetVertex(vert_2).DirectionTo(mdt.GetVertex(vert_2));

					if ((edge_a > edge_b) && (edge_a > edge_c))
					{
						removal.g = 1;
					}
					else if ((edge_b > edge_c) && (edge_b > edge_a))
					{
						removal.r = 1;
					}
					else
					{
						removal.b = 1;
					}

					if (colorChoices.Count > 0)
					{
						var next = colorChoices[0]; colorChoices.RemoveAt(0);
						vertColorStorage[vertIdx] = next;// + removal;
					}
					//JASON CLEANUP:  this else will never trigger, as there are only 3 verticies
					else
					{
						GD.Print("in else!");
						var coords2 = new List<Color>() { Colors.Red, Colors.Green, Colors.Blue };

						for (var m = 0; m < 3; m++)
						{
							if (m == faceVertId)
							{
								continue;
							}
							var vid2 = mdt.GetFaceVertex(faceIdx, m);
							if (vertColorStorage.ContainsKey(vid2))
							{
								coords2.Remove(vertColorStorage[vid2]);
							}
							vertColorStorage[vertIdx] = coords2[0] + removal; //BUG?  coords was  checked to not have any....  maybe means coords2
							coords2.RemoveAt(0);
						}
					}
					mdt.SetVertexColor(vertIdx, vertColorStorage[vertIdx]);

				}
			}
		}

	}

	private static void _CYBERREALITY(List<VertexInfo> sortedVerts, Color[] colorChoices, MeshDataTool mdt)
	{
		var done = new Dictionary<int, bool>();//vertidx/isDone
		var bary = new Dictionary<int, Color>(); //vertidx/color
		var rand = new Random(0);
		var rand_color = new Color((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble());
		var nors = new Dictionary<int, (Vector3 normal, Color color)>(); //normalIdx/normal
		for (var j = 0; j < mdt.GetFaceCount(); j++)
		{
			var fid = mdt.GetFaceVertex(j, 0);
			var nor = mdt.GetVertexNormal(fid);
			var coords = new List<Color>() { Colors.Red, Colors.Green, Colors.Blue };
			foreach (var n in nors.Keys)
			{
				var dot = nor.Dot(nors[n].normal);
				if (dot == 1)
				{
					rand_color = nors[n].color;
				}
				else
				{
					rand_color = new Color((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble());
				}
			}
			nors[fid] = (normal: nor, color: rand_color);
			for (var k = 0; k < 3; k++)
			{
				var vid = mdt.GetFaceVertex(j, k);
				if (bary.ContainsKey(vid))
				{
					coords.Remove(bary[vid]);
				}
			}
			for (var i = 0; i < 3; i++)
			{
				var vid = mdt.GetFaceVertex(j, i);
				if (!done.ContainsKey(vid) || done[vid] != true)
				{
					done[vid] = true;
					var removal = Colors.Black;

					var vert_0 = mdt.GetFaceVertex(j, 0);
					var vert_1 = mdt.GetFaceVertex(j, 1);
					var vert_2 = mdt.GetFaceVertex(j, 2);
					var edge_a = mdt.GetVertex(vert_2).DirectionTo(mdt.GetVertex(vert_0));
					var edge_b = mdt.GetVertex(vert_2).DirectionTo(mdt.GetVertex(vert_1));
					var edge_c = mdt.GetVertex(vert_2).DirectionTo(mdt.GetVertex(vert_2));

					if ((edge_a > edge_b) && (edge_a > edge_c))
					{
						removal.g = 1;
					}
					else if ((edge_b > edge_c) && (edge_b > edge_a))
					{
						removal.r = 1;
					}
					else
					{
						removal.b = 1;
					}

					if (coords.Count > 0)
					{
						var next = coords[0]; coords.RemoveAt(0);
						bary[vid] = next + removal;
					}
					else
					{
						var coords2 = new List<Color>() { Colors.Red, Colors.Green, Colors.Blue };

						for (var m = 0; m < 3; m++)
						{
							if (m == i)
							{
								continue;
							}
							var vid2 = mdt.GetFaceVertex(j, m);
							if (bary.ContainsKey(vid2))
							{
								coords2.Remove(bary[vid2]);
							}
							bary[vid] = coords2[0] + removal; //BUG?  coords was  checked to not have any....  maybe means coords2
							coords2.RemoveAt(0);
						}
					}
					mdt.SetVertexColor(vid, bary[vid]);

				}
			}
		}

	}


	private static void _GREEDY_FACE(List<VertexInfo> sortedVerts, Color[] colorChoices, MeshDataTool mdt)
	{
		List<VertexInfo> noColor = new List<VertexInfo>();
		for (int i = sortedVerts.Count - 1; i >= 0; i--)
		{
			var removed = false;
			var vertInfo = sortedVerts[i];

			for (var h = 0; h < colorChoices.Length; h++)
			{
				var color = colorChoices[h];
				if (vertInfo.TrySetAvailableColor(color))
				{
					sortedVerts.RemoveAt(i);
					removed = true;

					foreach (var adj0Vert in vertInfo.GetAdjacentVertInfo())
					{
						if (h < colorChoices.Length - 2 && adj0Vert.adjacentVerticies.Length >= 3 //vertInfo.adjacentVerticies.Length
						)
						{
							if (adj0Vert.TrySetAvailableColor(colorChoices[h + 1])) ;

						}
						if (h < colorChoices.Length - 3 && adj0Vert.adjacentVerticies.Length >= 3 //vertInfo.adjacentVerticies.Length
							)
						{
							adj0Vert.TrySetAvailableColor(colorChoices[h + 2]);
						}

						foreach (var adj1Vert in adj0Vert.GetAdjacentVertInfo())
						{
							if (adj1Vert.adjacentVerticies.Length >= 3) //vertInfo.adjacentVerticies.Length)
																		//if (adj1Vert.adjacentVerticies.Length >= vertInfo.adjacentVerticies.Length)
							{
								adj1Vert.TrySetAvailableColor(color);
							}
						}
					}


					break;
				}
			}
			if (removed == false)
			{
				noColor.Add(vertInfo);
				//var alphaBlack = new Color(0, 0, 0, 0);
				vertInfo.TrySetAvailableColor(Colors.White, true);
				sortedVerts.RemoveAt(i);
			}


		}

		GD.Print($"_GREEDY_FACE uncolored count={noColor.Count} / {mdt.GetVertexCount()}");
	}




	private static void _GREEDY_BASIC(List<VertexInfo> sortedVerts, Color[] colorChoices, MeshDataTool mdt)
	{

		bool TrySetAdjAdj(VertexInfo _current, Color c, List<VertexInfo> _store)
		{
			if (_store.Contains(_current) == false)
			{
				return true;
			}
			if (_current.TrySetAvailableColor(c))
			{
				_store.Remove(_current);
				foreach (var adj0Vert in _current.GetAdjacentVertInfo())
				{
					foreach (var adj1Vert in adj0Vert.GetAdjacentVertInfo())
					{
						TrySetAdjAdj(adj1Vert, c, _store);
					}
				}
				return true;
			}
			return false;
		}
		List<VertexInfo> problems = new List<VertexInfo>();
		while (sortedVerts.Count > 0)
		{
			var vertInfo = sortedVerts[sortedVerts.Count - 1];
			//sortedVerts.RemoveAt(sortedVerts.Count - 1);

			for (var h = 0; h < colorChoices.Length; h++)
			{
				var color = colorChoices[h];

				//TrySetAdjAdj(vertInfo, color, sortedVerts);



				if (vertInfo.TrySetAvailableColor(color))
				{
					sortedVerts.Remove(vertInfo);

					//preemptively try to set adjacent and adjadj with related colors
					foreach (var adj0Vert in vertInfo.GetAdjacentVertInfo())
					{
						if (h < colorChoices.Length - 2 // && adj0Vert.adjacentVerticies.Length >= vertInfo.adjacentVerticies.Length
							)
						{
							if (adj0Vert.TrySetAvailableColor(colorChoices[h + 1])) ;

						}
						if (h < colorChoices.Length - 3 //&& adj0Vert.adjacentVerticies.Length >= vertInfo.adjacentVerticies.Length
							)
						{
							adj0Vert.TrySetAvailableColor(colorChoices[h + 2]);
						}

						foreach (var adj1Vert in adj0Vert.GetAdjacentVertInfo())
						{
							//if (adj1Vert.adjacentVerticies.Length >= vertInfo.adjacentVerticies.Length)
							{
								adj1Vert.TrySetAvailableColor(color);
							}
						}
					}
					break;
				}

			}
			if (sortedVerts.Contains(vertInfo))
			{
				problems.Add(vertInfo);
				vertInfo.TrySetAvailableColor(Colors.White, true);
				sortedVerts.Remove(vertInfo);
			}
		}
		GD.Print($"_GREEDY_BASIC uncolored count={problems.Count} / {mdt.GetVertexCount()}");
	}

	private static void _WELSH_POWELL_ADJUSTED(List<VertexInfo> sortedVerts, Color[] colorChoices, MeshDataTool mdt)
	{
		for (var h = 0; h < colorChoices.Length; h++)
		{
			var color = colorChoices[h];


			//enumerate in reverse so we inspect our verticies with highest degree first (most edges)
			//and also lets us remove from the list directly 
			for (int i = sortedVerts.Count - 1; i >= 0; i--)
			{
				//if we remove too many, reset our index.   this means we might invoke this loop on an element more than once. 
				//but that's ok as it doesn't have negative consiquences.
				if (i >= sortedVerts.Count)
				{
					i = sortedVerts.Count - 1;
				}


				var vertInfo = sortedVerts[i];
				if (vertInfo.TrySetAvailableColor(color))
				{
					sortedVerts.RemoveAt(i);

					////make all verts at the same position the same color
					//foreach(var otherVert in vertStorage)
					//{
					//	if(otherVert.pos == vertInfo.pos)
					//	{
					//		otherVert.TrySetAvailableColor(color, true);
					//	}
					//}



					//preemptively try to set adjacent and adjadj with related colors
					foreach (var adj0Vert in vertInfo.GetAdjacentVertInfo())
					{
						//if (h < colorChoices.Length - 2 
						//	&& adj0Vert.adjacentVerticies.Length >=3
						//	//&& adj0Vert.adjacentVerticies.Length >= vertInfo.adjacentVerticies.Length
						//	)
						//{
						//	adj0Vert.TrySetAvailableColor(colorChoices[h + 1]);
						//}
						//if (h < colorChoices.Length - 3
						//	&& adj0Vert.adjacentVerticies.Length >= 3
						//	//&& adj0Vert.adjacentVerticies.Length >= vertInfo.adjacentVerticies.Length
						//	)
						//{
						//	adj0Vert.TrySetAvailableColor(colorChoices[h + 2]);
						//}


						foreach (var adj1Vert in adj0Vert.GetAdjacentVertInfo())
						{
							if (adj1Vert.adjacentVerticies.Length > vertInfo.adjacentVerticies.Length * 0.75)//vertInfo.adjacentVerticies.Length)
																											 //if (adj1Vert.adjacentVerticies.Length >= vertInfo.adjacentVerticies.Length)
							{
								adj1Vert.TrySetAvailableColor(color);
							}
						}
					}

				}
			}
		}
		//any remaining verts are uncolored!  bad.
		GD.Print($"ERROR!  Verticies uncolored.  count={sortedVerts.Count} / {mdt.GetVertexCount()}");

		//for any remaining verticies color alpha
		//var alphaBlack = new Color(0, 0, 0, 0);
		for (int i = sortedVerts.Count - 1; i >= 0; i--)
		{
			var vertInfo = sortedVerts[i];
			//mdt.SetVertexColor(vertInfo.vertIdx, alphaBlack);
			vertInfo.TrySetAvailableColor(Colors.White, true);
		}
	}

	/// <summary>
	/// 3rd attempt:  after researching a bit I realized this is a "graph coloring" problem: https://en.wikipedia.org/wiki/Graph_coloring
	/// nice overview of some here: https://github.com/Ajaypal91/Graph_Coloring_Algorithms_Implementation
	/// here we implement welsh-powell algorithm:  https://www.youtube.com/watch?v=CQIW2mLfG04
	/// </summary>
	/// <param name="mdt"></param>
	private static void _SetVertexColorToBarycentric_WP(MeshDataTool mdt)
	{
		//store info about our verticies into an array
		var vertStorage = new VertexInfo[mdt.GetVertexCount()];
		for (var vertIdx = 0; vertIdx < vertStorage.Length; vertIdx++)
		{
			vertStorage[vertIdx] = new VertexInfo(vertIdx, mdt, vertStorage);
			//set vert color to black
			mdt.SetVertexColor(vertIdx, Colors.Transparent);
		}

		//sort verticies by degree (number of edges).   defaults to highest first
		var sortedVerts = new List<VertexInfo>(vertStorage);
		sortedVerts.Sort();
		//verts.CopyTo(sortedVerts,0);
		//Array.Sort(sortedVerts);

		var colorChoices = new Color[] { Colors.Red, Colors.Green, Colors.Blue };
		foreach (var color in colorChoices)
		{
			//enumerate in reverse so we inspect our verticies with highest degree first (most edges)
			//and also lets us remove from the list directly 
			for (int i = sortedVerts.Count - 1; i >= 0; i--)
			{
				var vertInfo = sortedVerts[i];
				if (vertInfo.TrySetAvailableColor(color))
				{
					sortedVerts.RemoveAt(i);
				}
			}
		}

		//any remaining verts are uncolored!  bad.
		GD.Print($"ERROR!  Verticies uncolored.  count={sortedVerts.Count}");

		//for any remaining verticies color alpha
		var alphaBlack = new Color(0, 0, 0, 0);
		for (int i = sortedVerts.Count - 1; i >= 0; i--)
		{
			var vertInfo = sortedVerts[i];
			mdt.SetVertexColor(vertInfo.vertIdx, alphaBlack);
		}





	}

	private static void _SetVertexColorToBarycentric_old(MeshDataTool mdt)
	{

		//try looping verticies
		{
			// var marked = new System.Collections.Generic.Dictionary<string, Color>();
			// var markFailCount = 0;
			// var markIndex = 0;
			// float vertexCount = mdt.GetVertexCount();

			// for (var i = 0; i < vertexCount; i++)
			// {
			// 	var vertex = mdt.GetVertex(i);
			// 	var vertId = vertex.ToString("F2");
			// 	// if (marked.TryGetValue(vertId, out var markedColor))
			// 	// {
			// 	// 	GD.Print($"MARK FAIL!  {vertId}");
			// 	// 	mdt.SetVertexColor(i, markedColor);
			// 	// 	markFailCount++;
			// 	// 	continue;
			// 	// }

			// 	var channel = markIndex % 3;
			// 	var color = new Color(0, 0, 0, 1);
			// 	switch (channel)
			// 	{
			// 		case 0:
			// 			color.r = 1;
			// 			break;
			// 		case 1:
			// 			color.g = 1;
			// 			break;
			// 		case 2:
			// 			color.b = 1;
			// 			break;
			// 		case 3:
			// 			color.r = 1;
			// 			color.g = 1;
			// 			break;
			// 		case 4:
			// 			color.r = 1;
			// 			color.b = 1;
			// 			break;
			// 		case 5:
			// 			color.b = 1;
			// 			color.g = 1;
			// 			break;
			// 		case 6:
			// 			color.r = 1;
			// 			color.g = 1;
			// 			color.b = 1;
			// 			break;
			// 		case 7:
			// 			color.r = 0;
			// 			color.g = 0;
			// 			color.b = 0;
			// 			break;

			// 	}
			// 	color = new Color(0, 0, 0, 1);
			// 	//marked.Add(vertId, color);
			// 	markIndex++;
			// 	mdt.SetVertexColor(i, color);
			// }

		}

		//private helper, gets the name of a color (assumes R,G,B, or Black)
		string _getColorName(Color color)
		{
			switch (color)
			{
				case Color c when c == Colors.Red:
					return "Red";
					break;
				case Color c when c == Colors.Green:
					return "Green";
					break;
				case Color c when c == Colors.Blue:
					return "Blue";
					break;
				case Color c when c == Colors.Black:
					return "Black";
					break;
				default:
					throw new Exception($"getColorName, unknown color={color}.  This helper function supports only Red, Green,Blue, or Black.");
			}
		}

		{

		}




		//loop all faces, assigning colors to each of it's 3 verticies.
		{
			//var faceCount = mdt.GetFaceCount();
			//var faceColorChoices = new List<string>();
			//var vertClusterColors = new Dictionary<string, string>(); //key = vertex.toString(), value = color name

			////preprocess verticies of the mesh.
			////reset vertex color to black.  we do this in case the model already uses R,G, or B for vertex color, which can mess up our color assigning algorithm below (we check the color of the vertex to see if it's been set)
			//var vertexCount = mdt.GetVertexCount();
			//for (var vertIdx = 0; vertIdx < vertexCount; vertIdx++)
			//{
			//	mdt.SetVertexColor(vertIdx, Colors.Black);
			//}

			////first loop through all faces, assign red to the vertex with the most linked faces, black to others.
			////this is to reduce chance of the green or blue colors "running out" due to other verticies being shared by faces.
			//for (var faceIdx = 0; faceIdx < faceCount; faceIdx++)
			//{
			//	//break;

			//	//the colors this faces' verticies will use. 
			//	faceColorChoices.Clear();
			//	faceColorChoices.Add("Green");
			//	faceColorChoices.Add("Blue");
			//	faceColorChoices.Add("Red");


			//	//sort verts by number of linked faces, and remove any used colors
			//	var mostFacedVertFaces = 0;
			//	var mostFacedVertIdx = -1;
			//	//var sortedButUncoloredVerts = new SortedS<int, int>(); //key = faceCount, value = vertIdx
			//	// { int faces; int vertIdx } tp;// = { faces = 1, vertIdx = -1 };

			//	for (var faceVert = 0; faceVert < 3; faceVert++)
			//	{
			//		var vertIdx = mdt.GetFaceVertex(faceIdx, faceVert);
			//		var faces = mdt.GetVertexFaces(vertIdx);
			//		var vertColor = mdt.GetVertexColor(vertIdx);
			//		var vertColorName = _getColorName(vertColor);



			//		switch (vertColorName)
			//		{
			//			case "Black":
			//				//no color assigned to the vertex
			//				if (faces.Length > mostFacedVertFaces)
			//				{
			//					mostFacedVertFaces = faces.Length;
			//					mostFacedVertIdx = vertIdx;
			//				}
			//				//sortedButUncoloredVerts.Add(faces.Length, vertIdx);  //only add the vert if it's uncolored
			//				break;
			//			default:
			//				//remove the vertex's color from our choices (so the next loop through the faces's verticies wont pick it)
			//				var result = faceColorChoices.Remove(vertColorName);
			//				if (!result)
			//				{
			//					//color used twice in the vertex!  bad
			//					GD.Print($"failed, vert color used twice!!!!!  color={vertColorName} vertIdx={vertIdx}");
			//					//throw new Exception($"failed to set the faces vertex color properly.  color is used twice on the face.");
			//				}
			//				break;
			//		}
			//	}
			//	//assign color to our top uncolored vert
			//	{
			//		//var vertIdx = sortedButUncoloredVerts.
			//		if (mostFacedVertFaces > 2)
			//		{

			//			var vert = mdt.GetVertex(mostFacedVertIdx);
			//			var vertName = vert.ToString("F5");

			//			var colorChoice = faceColorChoices[0];
			//			faceColorChoices.RemoveAt(0);
			//			mdt.SetVertexColor(mostFacedVertIdx, Color.ColorN(colorChoice));
			//			if (!vertClusterColors.ContainsKey(vertName))
			//			{
			//				vertClusterColors[vertName] = colorChoice;
			//			}
			//		}
			//	}






			//	//	var mostFacedVertFaces = 0;
			//	//var mostFacedVert = -1;
			//	//var isRedAssigned = false;
			//	//var isBlueAssigned = false;
			//	//for (var faceVert = 0; faceVert < 3; faceVert++)
			//	//{
			//	//	var vertIdx = mdt.GetFaceVertex(faceIdx, faceVert);
			//	//	var vertColor = mdt.GetVertexColor(vertIdx);
			//	//	var vertColorName = _getColorName(vertColor);
			//	//	switch (vertColorName)
			//	//	{
			//	//		case "Red":
			//	//			isRedAssigned = true;
			//	//			break;
			//	//		case "Blue":
			//	//			isBlueAssigned = true;
			//	//			break;
			//	//	}

			//	//	var faces = mdt.GetVertexFaces(vertIdx);
			//	//	if (mostFacedVertFaces >= faces.Length)
			//	//	{
			//	//		continue;
			//	//	}
			//	//	mostFacedVertFaces = faces.Length;
			//	//	mostFacedVert = faceVert;
			//	//}




			//}


			//for (var faceIdx = 0; faceIdx < faceCount; faceIdx++)
			//{
			//	//the colors this faces' verticies will use. 
			//	faceColorChoices.Clear();
			//	faceColorChoices.Add("Red");
			//	faceColorChoices.Add("Green");
			//	faceColorChoices.Add("Blue");


			//	//loop through all the faces' verticies and removed any colors already used.
			//	for (var faceVert = 0; faceVert < 3; faceVert++)
			//	{
			//		var vertIdx = mdt.GetFaceVertex(faceIdx, faceVert);
			//		//var vert = mdt.GetVertex(vertIdx);
			//		//var vertName = vert.ToString("F5");
			//		var vertColor = mdt.GetVertexColor(vertIdx);
			//		var vertColorName = _getColorName(vertColor);
			//		switch (vertColorName)
			//		{
			//			case "Black":
			//				//no color assigned to the vertex
			//				break;
			//			default:
			//				//remove the vertex's color from our choices (so the next loop through the faces's verticies wont pick it)
			//				var result = faceColorChoices.Remove(vertColorName);
			//				if (!result)
			//				{
			//					//color used twice in the vertex!  bad
			//					GD.Print($"failed, vert color used twice!!!!!  color={vertColorName} vertIdx={vertIdx}");
			//					//throw new Exception($"failed to set the faces vertex color properly.  color is used twice on the face.");
			//				}
			//				break;
			//		}
			//	}



			//	//loop through all the faces' verticies and assign color used by the vertCluster
			//	for (var faceVert = 0; faceVert < 3; faceVert++)
			//	{
			//		var vertIdx = mdt.GetFaceVertex(faceIdx, faceVert);
			//		var vert = mdt.GetVertex(vertIdx);
			//		var vertName = vert.ToString("F5");
			//		var vertColor = mdt.GetVertexColor(vertIdx);
			//		var vertColorName = _getColorName(vertColor);
			//		switch (vertColorName)
			//		{
			//			case "Black":
			//				//no color assigned to the vertex
			//				//so check if another another vertex with same location has this color, so use it too.
			//				if (vertClusterColors.TryGetValue(vertName, out var _tempVertCol))
			//				{								
			//					if (faceColorChoices.Remove(_tempVertCol))
			//					{
			//						mdt.SetVertexColor(vertIdx, Color.ColorN(_tempVertCol));									
			//					}
			//				}
			//				break;
			//			default:
			//				//vert already colored
			//				break;
			//		}
			//	}



			//	//this pass, assign all black verticies our remaining colors
			//	for (var faceVert = 0; faceVert < 3; faceVert++)
			//	{
			//		var vertIdx = mdt.GetFaceVertex(faceIdx, faceVert);
			//		var vert = mdt.GetVertex(vertIdx);
			//		var vertName = vert.ToString("F5");
			//		var vertColor = mdt.GetVertexColor(vertIdx);
			//		var vertColorName = _getColorName(vertColor);
			//		switch (vertColorName)
			//		{
			//			case "Black":
			//				//assign the vertex a color from our remaining color options for this vertex,
			//				//but we prefer using a color that other verticies at the same location are already using.

			//				string colorChoice = null;
			//				if (vertClusterColors.TryGetValue(vertName, out var _tempVertCol))
			//				{
			//					if (faceColorChoices.Remove(_tempVertCol))
			//					{
			//						//another vertex with same location has this color, so use it too.
			//						colorChoice = _tempVertCol;
			//					}
			//					else
			//					{
			//						//color already in use by another vertex, so do our "normal" color pick
			//					}
			//				}

			//				if (colorChoice is null)
			//				{
			//					//
			//					colorChoice = faceColorChoices[0];
			//					faceColorChoices.RemoveAt(0);
			//				}


			//				mdt.SetVertexColor(vertIdx, Color.ColorN(colorChoice));
			//				if (!vertClusterColors.ContainsKey(vertName))
			//				{
			//					vertClusterColors[vertName] = colorChoice;
			//				}


			//				break;
			//			default:
			//				//vertex already has a color assigned
			//				break;
			//		}


			//		//switch (faceVert)
			//		//{
			//		//	case 0:
			//		//		vertColor.r = 1;
			//		//		break;
			//		//	case 1:
			//		//		vertColor.g = 1;
			//		//		break;
			//		//	case 2:
			//		//		vertColor.b = 1;
			//		//		break;
			//		//}
			//		//mdt.SetVertexColor(vertIdx, vertColor);






			//		//if (vertColors.TryGetValue(vertName, out var _tempVertCol))
			//		//{
			//		//	mdt.getvertexc
			//		//	//if this vertex already has a color assigned to it
			//		//	vertColor = _tempVertCol;
			//		//}
			//		//else
			//		//{
			//		//	switch (priorVertColor)
			//		//	{
			//		//		case Color c when c == Colors.Black || c == Colors.Blue:
			//		//			vertColor = Colors.Red;
			//		//			break;
			//		//		case Color c when c == Colors.Red:
			//		//			vertColor = Colors.Green;
			//		//			break;
			//		//		case Color c when c == Colors.Green:
			//		//			vertColor = Colors.Blue;
			//		//			break;
			//		//	}

			//		//	mdt.SetVertexColor(vert0Idx, Colors.Red);
			//		//	lastVertCol = Colors.Red;
			//		//}

			//		////verify that color isn't already used for a vert of this face.
			//		//switch (vertColor)
			//		//{
			//		//	case Color c when c == Colors.Red:
			//		//		if (redUsed)
			//		//		{
			//		//			throw new Exception("red already used");
			//		//		}
			//		//		redUsed = true;
			//		//		break;
			//		//	case Color c when c == Colors.Green:
			//		//		if (greenUsed)
			//		//		{
			//		//			throw new Exception("green already used");
			//		//		}
			//		//		greenUsed = true;
			//		//		break;
			//		//	case Color c when c == Colors.Blue:
			//		//		if (blueUsed)
			//		//		{
			//		//			throw new Exception("blue already used");
			//		//		}
			//		//		blueUsed = true;
			//		//		break;
			//		//}

			//	}

			//}
		}
	}


}
