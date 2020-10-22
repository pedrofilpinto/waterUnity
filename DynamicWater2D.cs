using System.Collections;
using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine;

//The wave stuff is not mine. by wave stuff i mean the mesh and its respective physics
//but the buoyancy and inwater physics are...

namespace ilhamhe {

	public class DynamicWater2D : MonoBehaviour {

		[System.Serializable]
		public struct Bound {
			public float top;
			public float right;
			public float bottom;
			public float left;
		}
        public Player player;

		[Header ("Water Settings")]
		public Bound bound;
		public int quality;

		public Material waterMaterial;
		public GameObject splash;
		public GameObject waterBubbles;

		private Vector3[] vertices;

		private List<Rigidbody2D> allCols;

		private Mesh mesh;

		[Header ("Physics Settings")]
		public float springconstant = 0.02f;
		public float damping = 0.1f;
		public float spread = 0.1f;
		public float collisionVelocityFactor = 0.04f;

		float rotationMagn = 1;

		float[] velocities;
		float[] accelerations;
		float[] leftDeltas;
		float[] rightDeltas;
        private float timeExit=1;
        private bool leftWater = true;

		float height;
		private float timer;

		private void Start () {
			InitializePhysics ();
			GenerateMesh ();
			SetBoxCollider2D ();
			allCols = new List<Rigidbody2D>();
			height = GetComponent<Renderer>().bounds.size.y;
		}

		private void InitializePhysics () {
			velocities = new float[quality];
			accelerations = new float[quality];
			leftDeltas = new float[quality];
			rightDeltas = new float[quality];
		}

		private void GenerateMesh () {
			float range = (bound.right - bound.left) / (quality - 1);
			vertices = new Vector3[quality * 2];

			// generate vertices
			// top vertices
			for (int i = 0; i < quality; i++) {
				vertices[i] = new Vector3 (bound.left + (i * range), bound.top, 0);
			}
			// bottom vertices
			for (int i = 0; i < quality; i++) {
				vertices[i + quality] = new Vector2 (bound.left + (i * range), bound.bottom);
			}

			// generate tris. the algorithm is messed up but works. lol.
			int[] template = new int[6];
			template[0] = quality;
			template[1] = 0;
			template[2] = quality + 1;
			template[3] = 0;
			template[4] = 1;
			template[5] = quality + 1;

			int marker = 0;
			int[] tris = new int[((quality - 1) * 2) * 3];
			for (int i = 0; i < tris.Length; i++) {
				tris[i] = template[marker++]++;
				if (marker >= 6) marker = 0;
			}

			// generate mesh
			MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer> ();
			if (waterMaterial) meshRenderer.sharedMaterial = waterMaterial;

			MeshFilter meshFilter = gameObject.AddComponent<MeshFilter> ();

			mesh = new Mesh ();
			mesh.vertices = vertices;
			mesh.triangles = tris;
			mesh.RecalculateNormals ();
			mesh.RecalculateBounds ();

			// set up mesh
			meshFilter.mesh = mesh;
		}

		private void SetBoxCollider2D () {
			BoxCollider2D col = gameObject.AddComponent<BoxCollider2D> ();
			col.isTrigger = true;
		}

		private void Update () {
			// optimization. we don't want to calculate all of this on every update.
			if(timer <= 0) return;
			timer -= Time.deltaTime;

			
            if (leftWater)
                CounterAfterExit();
			// updating physics
			for (int i = 0; i < quality; i++) {
				float force = springconstant * (vertices[i].y - bound.top) + velocities[i] * damping;
				accelerations[i] = -force;
				vertices[i].y += velocities[i];
				velocities[i] += accelerations[i];
			}

			for (int i = 0; i < quality; i++) {
				if (i > 0) {
					leftDeltas[i] = spread * (vertices[i].y - vertices[i - 1].y);
					velocities[i - 1] += leftDeltas[i];
				}
				if (i < quality - 1) {
					rightDeltas[i] = spread * (vertices[i].y - vertices[i + 1].y);
					velocities[i + 1] += rightDeltas[i];
				}
			}

			// updating mesh
			mesh.vertices = vertices;
		}

        private void FixedUpdate()
        {
			calculateAllColliders();
		}

        private void OnTriggerEnter2D(Collider2D col)
        {
            
            if (col.gameObject.name.Equals("Player") )
            {
                player.inWater = true;
                leftWater = false;	
				Splash(col, player.velocity.y * collisionVelocityFactor);
            }    
            else
            {
                Rigidbody2D rb = col.GetComponent<Rigidbody2D>();
				allCols.Add(rb);
				AddDragCoef(rb, col);
				Splash(col, rb.velocity.y * collisionVelocityFactor);
            }
        }

        void OnTriggerExit2D(Collider2D col)
        {
            if (col.gameObject.name.Equals("Player"))
            {
                player.inWater = false;
                leftWater = true;
				Splash(col, player.velocity.y * collisionVelocityFactor);
            }
            else
            {
                Rigidbody2D rb = col.GetComponent<Rigidbody2D>();
                rb.drag = 1;
				allCols.Remove(rb);
				Splash(col, rb.velocity.y * collisionVelocityFactor);
            }
        }

        public void Splash (Collider2D col, float force) {
			timer = 10f;
			float radius = col.bounds.max.x - col.bounds.min.x;
			Vector2 center = new Vector2(col.bounds.center.x, bound.top) ;
			// instantiate splash particle
			//Debug.Log("Force!: " + force);

			if (force < -1f)
            {
				if (col.gameObject.name.Equals("Player"))
				{
					GameObject splashGO = Instantiate(splash, new Vector3(center.x, center.y, 0), Quaternion.Euler(0, 0, 60));
					Destroy(splashGO, 2f);
				}
				else
				{
					GameObject splashGO = Instantiate(splash, new Vector3(col.bounds.center.x, col.bounds.center.y, 0), Quaternion.Euler(0, 0, 60));
					//Instantiate(waterBubbles, new Vector2(col.bounds.center.x, col.bounds.center.y - (col.bounds.size.y * 2f)), Quaternion.Euler(0, 0, 0));
					Destroy(splashGO, 2f);
				}
			}
			// applying physics
			for (int i = 0; i < quality; i++) {
				if (PointInsideCircle (vertices[i], center, radius)) {
					velocities[i] = force*col.attachedRigidbody.mass*0.1f;			
				}
			}
		}

		bool PointInsideCircle (Vector2 point, Vector2 center, float radius) {
			return Vector2.Distance (point, center) < radius;
		}

        private void CounterAfterExit()
        {
            if (timeExit > 0)
            {
                timeExit -= Time.deltaTime;
            }
            else
            {
                player.wasStopped = false;
                timeExit = 1;
            }  
        }

		private void AddDragCoef(Rigidbody2D col, Collider2D colC)
        {
			for (;col.drag < 8/col.mass;)
            {
				col.drag+=0.1f/col.mass*20f;
			}			
        }

		//BOUYANCY OR WHATEVER HACKIEST SHIT EVER. I SWEAR IT LEGITIMATLY HURTS JUST THINKING ABOUT THIS FUCKTION!
		//October 20 /2020- the fucking object floats aswell as a rock does...
		//October 21 /2020- the fucking object floats... or sort of...
		//October 22 /2020- the fucking object float but it looks like a fidget fucking spinner, smh...
		private void calculateAllColliders()
        {
            if (rotationMagn < 0) 
				rotationMagn*=-1;
			for (int i = 0; i < allCols.Count; i++)
            {
				Rigidbody2D newCollider = allCols[i];
				float colwidth = newCollider.GetComponent<Renderer>().bounds.size.x;
				if (newCollider.mass < 1 && newCollider.transform.up.y > 0)
				{
					//newCollider.position = Vector2.MoveTowards(newCollider.position, new Vector2(newCollider.position.x, this.transform.position.y), 3 * Time.deltaTime);
					//newCollider.AddForce(Vector2.up * newCollider.mass * colwidth*0.2f, ForceMode2D.Impulse);
					//Debug.Log(360-newCollider.transform.eulerAngles.z);

					if (newCollider.transform.eulerAngles.z > 0 && newCollider.transform.eulerAngles.z < 180)
					{
						//newCollider.AddForceAtPosition(Vector2.up * newCollider.mass * 2f * -newCollider.transform.eulerAngles.z*0.7f, new Vector2(newCollider.position.x - colwidth / 2f, 0), ForceMode2D.Impulse);
						newCollider.AddForceAtPosition(Vector2.up * newCollider.transform.eulerAngles.z*colwidth*0.1f, new Vector2(newCollider.position.x - colwidth / 2f, 0), ForceMode2D.Force);
						newCollider.AddForceAtPosition(Vector2.up * (newCollider.transform.eulerAngles.z/2f) * colwidth * 0.1f, new Vector2(newCollider.position.x - colwidth / 4f, 0), ForceMode2D.Force);
					}
					

					Debug.Log(newCollider.transform.eulerAngles.z);
					if (newCollider.transform.eulerAngles.z > 180 && newCollider.transform.eulerAngles.z <= 360)
					{
						newCollider.AddForceAtPosition(Vector2.up * ((360 - newCollider.transform.eulerAngles.z)) * colwidth * 0.1f, new Vector2(newCollider.position.x + colwidth / 2f, 0), ForceMode2D.Force);
						//newCollider.AddForceAtPosition(Vector2.up * newCollider.mass * 2f * -newCollider.transform.eulerAngles.z * 0.7f, new Vector2(newCollider.position.x + colwidth / 2f, 0), ForceMode2D.Impulse);
						newCollider.AddForceAtPosition(Vector2.up * ((360 - newCollider.transform.eulerAngles.z)/2f) * colwidth * 0.1f, new Vector2(newCollider.position.x + colwidth / 4f, 0), ForceMode2D.Force);
					}
										
					if(newCollider.position.y < this.transform.position.y)
						newCollider.AddForce(Vector2.up * newCollider.mass * 2f, ForceMode2D.Impulse);
				}
				//If the object somehow flips itself upside down because why the fuck not amirite?
				if (newCollider.mass < 1 && newCollider.transform.up.y < 0)
				{
					//newCollider.position = Vector2.MoveTowards(newCollider.position, new Vector2(newCollider.position.x, this.transform.position.y), 3 * Time.deltaTime);
					//newCollider.AddForce(Vector2.up * newCollider.mass * colwidth*0.2f, ForceMode2D.Impulse);
					//Debug.Log(360-newCollider.transform.eulerAngles.z);

					if (newCollider.transform.eulerAngles.z > 0 && newCollider.transform.eulerAngles.z < 180)
					{
						//newCollider.AddForceAtPosition(Vector2.up * newCollider.mass * 2f * -newCollider.transform.eulerAngles.z*0.7f, new Vector2(newCollider.position.x - colwidth / 2f, 0), ForceMode2D.Impulse);
						newCollider.AddForceAtPosition(Vector2.up * newCollider.transform.eulerAngles.z * colwidth * 0.1f, new Vector2(newCollider.position.x + colwidth / 2f, 0), ForceMode2D.Force);
						newCollider.AddForceAtPosition(Vector2.up * (newCollider.transform.eulerAngles.z / 2f) * colwidth * 0.1f, new Vector2(newCollider.position.x + colwidth / 4f, 0), ForceMode2D.Force);
					}


					Debug.Log(newCollider.transform.eulerAngles.z);
					if (newCollider.transform.eulerAngles.z > 180 && newCollider.transform.eulerAngles.z <= 360)
					{
						newCollider.AddForceAtPosition(Vector2.up * ((360 - newCollider.transform.eulerAngles.z)) * colwidth * 0.1f, new Vector2(newCollider.position.x - colwidth / 2f, 0), ForceMode2D.Force);
						//newCollider.AddForceAtPosition(Vector2.up * newCollider.mass * 2f * -newCollider.transform.eulerAngles.z * 0.7f, new Vector2(newCollider.position.x + colwidth / 2f, 0), ForceMode2D.Impulse);
						newCollider.AddForceAtPosition(Vector2.up * ((360 - newCollider.transform.eulerAngles.z) / 2f) * colwidth * 0.1f, new Vector2(newCollider.position.x - colwidth / 4f, 0), ForceMode2D.Force);
					}

					if (newCollider.position.y < this.transform.position.y)
						newCollider.AddForce(Vector2.up * newCollider.mass * 2f, ForceMode2D.Impulse);
				}
			}
        }
      
	}

}