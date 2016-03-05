/**
 * Class representing a 3D solid.
 *  
 * original author: Danilo Balby Silva Castanheira (danbalby@yahoo.com)
 * 
 * Ported from Java to C# by Sebastian Loncar, Web: http://loncar.de
 * Project: https://github.com/Arakis/Net3dBool
 */

using System;
using System.IO;
using System.Collections;
using System.Linq;
using System.Collections.Generic;

namespace Net3dBool
{


    public class Solid : Shape3D
    {
        /** array of indices for the vertices from the 'vertices' attribute */
        protected int[] indices;
        /** array of points defining the solid's vertices */
        protected Point3d[] vertices;

        //--------------------------------CONSTRUCTORS----------------------------------//

        /** Constructs an empty solid. */           
        public Solid()
        {
            setInitialFeatures();
        }

        /**
     * Construct a solid based on data arrays. An exception may occur in the case of 
     * abnormal arrays (indices making references to inexistent vertices, there are less
     * colors than vertices...)
     * 
     * @param vertices array of points defining the solid vertices
     * @param indices array of indices for a array of vertices
     * @param colors array of colors defining the vertices colors 
     */
        public Solid(Point3d[] vertices, int[] indices)
            : this()
        {
            setData(vertices, indices);
        }

        /** Sets the initial features common to all constructors */
        protected void setInitialFeatures()
        {
            vertices = new Point3d[0];
            indices = new int[0];

//            setCapability(Shape3D.ALLOW_GEOMETRY_WRITE);
//            setCapability(Shape3D.ALLOW_APPEARANCE_WRITE);
//            setCapability(Shape3D.ALLOW_APPEARANCE_READ);
        }

        //---------------------------------------GETS-----------------------------------//

        /**
     * Gets the solid vertices
     * 
     * @return solid vertices
     */ 
        public Point3d[] getVertices()
        {
            Point3d[] newVertices = new Point3d[vertices.Length];
            for (int i = 0; i < newVertices.Length; i++)
            {
                newVertices[i] = vertices[i];
            }
            return newVertices;
        }

        /** Gets the solid indices for its vertices
     * 
     * @return solid indices for its vertices
     */
        public int[] getIndices()
        {
            int[] newIndices = new int[indices.Length];
            Array.Copy(indices, 0, newIndices, 0, indices.Length);
            return newIndices;
        }

        /**
     * Gets if the solid is empty (without any vertex)
     * 
     * @return true if the solid is empty, false otherwise
     */
        public bool isEmpty()
        {
            if (indices.Length == 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        //---------------------------------------SETS-----------------------------------//

        /**
     * Sets the solid data. Each vertex may have a different color. An exception may 
     * occur in the case of abnormal arrays (e.g., indices making references to  
     * inexistent vertices, there are less colors than vertices...)
     * 
     * @param vertices array of points defining the solid vertices
     * @param indices array of indices for a array of vertices
     * @param colors array of colors defining the vertices colors 
     */
        public void setData(Point3d[] vertices, int[] indices)
        {
            this.vertices = new Point3d[vertices.Length];
            this.indices = new int[indices.Length];
            if (indices.Length != 0)
            {
                for (int i = 0; i < vertices.Length; i++)
                {
                    this.vertices[i] = vertices[i].Clone();
                }
                Array.Copy(indices, 0, this.indices, 0, indices.Length);

                defineGeometry();
            }
        }

        //-------------------------GEOMETRICAL_TRANSFORMATIONS-------------------------//

        /**
     * Applies a translation into a solid
     * 
     * @param dx translation on the x axis
     * @param dy translation on the y axis
     */
        public void translate(double dx, double dy)
        {
            if (dx != 0 || dy != 0)
            {
                for (int i = 0; i < vertices.Length; i++)
                {
                    vertices[i].x += dx;
                    vertices[i].y += dy;
                }

                defineGeometry();
            }
        }

        /**
     * Applies a rotation into a solid
     * 
     * @param dx rotation on the x axis
     * @param dy rotation on the y axis
     */
        public void rotate(double dx, double dy)
        {
            double cosX = Math.Cos(dx);
            double cosY = Math.Cos(dy);
            double sinX = Math.Sin(dx);
            double sinY = Math.Sin(dy);

            if (dx != 0 || dy != 0)
            {
                //get mean
                Point3d mean = getMean();

                double newX, newY, newZ;
                for (int i = 0; i < vertices.Length; i++)
                {
                    vertices[i].x -= mean.x; 
                    vertices[i].y -= mean.y; 
                    vertices[i].z -= mean.z; 

                    //x rotation
                    if (dx != 0)
                    {
                        newY = vertices[i].y * cosX - vertices[i].z * sinX;
                        newZ = vertices[i].y * sinX + vertices[i].z * cosX;
                        vertices[i].y = newY;
                        vertices[i].z = newZ;
                    }

                    //y rotation
                    if (dy != 0)
                    {
                        newX = vertices[i].x * cosY + vertices[i].z * sinY;
                        newZ = -vertices[i].x * sinY + vertices[i].z * cosY;
                        vertices[i].x = newX;
                        vertices[i].z = newZ;
                    }

                    vertices[i].x += mean.x; 
                    vertices[i].y += mean.y; 
                    vertices[i].z += mean.z;
                }
            }

            defineGeometry();
        }

        /**
     * Applies a zoom into a solid
     * 
     * @param dz translation on the z axis
     */
        public void zoom(double dz)
        {
            if (dz != 0)
            {
                for (int i = 0; i < vertices.Length; i++)
                {
                    vertices[i].z += dz;
                }

                defineGeometry();
            }
        }

        /**
     * Applies a scale changing into the solid
     * 
     * @param dx scale changing for the x axis 
     * @param dy scale changing for the y axis
     * @param dz scale changing for the z axis
     */
        public void scale(double dx, double dy, double dz)
        {
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i].x *= dx;
                vertices[i].y *= dy;
                vertices[i].z *= dz;
            }

            defineGeometry();
        }

        //-----------------------------------PRIVATES--------------------------------//

        /** Creates a geometry based on the indexes and vertices set for the solid */
        protected void defineGeometry()
        {
//            GeometryInfo gi = new GeometryInfo(GeometryInfo.TRIANGLE_ARRAY);
//            gi.setCoordinateIndices(indices);
//            gi.setCoordinates(vertices);
//            NormalGenerator ng = new NormalGenerator();
//            ng.generateNormals(gi);
//
//            gi.setColors(colors);
//            gi.setColorIndices(indices);
//            gi.recomputeIndices();
//
//            setGeometry(gi.getIndexedGeometryArray());
        }

        /**
     * Gets the solid mean
     * 
     * @return point representing the mean
     */
        protected Point3d getMean()
        {
            Point3d mean = new Point3d();
            for (int i = 0; i < vertices.Length; i++)
            {
                mean.x += vertices[i].x;
                mean.y += vertices[i].y;
                mean.z += vertices[i].z;
            }
            mean.x /= vertices.Length;
            mean.y /= vertices.Length;
            mean.z /= vertices.Length;

            return mean;
        }
    }
}

