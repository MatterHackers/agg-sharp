// Derived from code on https://github.com/BorisTheBrave/mc-dc
// CC0 BorisTheBrave
// C# port by Lars Brubaker


using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;

namespace DualContouring
{
#if false
    public class Quad
    {
        public int v1;
        public int v2;
        public int v3;
        public int v4;

        // A 3d quadrilateral (polygon with 4 vertices)
        public Quad(int v1, int v2, int v3, int v4)
        {
            this.v1 = v1;
            this.v2 = v2;
            this.v3 = v3;
            this.v4 = v4;
        }

        public Quad map(Func<int, int> f)
        {
            return new Quad(f(this.v1), f(this.v2), f(this.v3), f(this.v4));
        }

        public Quad swap(bool swap = true)
        {
            if (swap)
                return new Quad(this.v4, this.v3, this.v2, this.v1);
            else
                return new Quad(this.v1, this.v2, this.v3, this.v4);
        }
    }

    public static class Settings
    {
        // Stores some global settings, mostly to save on a lot of repetitive arguments.
        // These are mostly of interest for demonstrating different variants of the algorithms as discussed
        // in the accompanying article.


        // Both marching cube and dual contouring are adaptive, i.e. they select
        // the vertex that best describes the underlying function. But for illustrative purposes
        // you can turn this off, and simply select the midpoint vertex.
        public static bool ADAPTIVE = true;

        // In dual contouring, if true, crudely force the selected vertex to belong in the cell
        public static bool CLIP = false;
        // In dual contouring, if true, apply boundaries to the minimization process finding the vertex for each cell
        public static bool BOUNDARY = true;
        // In dual contouring, if true, apply extra penalties to encourage the vertex to stay within the cell
        public static bool BIAS = true;
        // Strength of the above bias, relative to 1.0 strength for the input gradients
        public static double BIAS_STRENGTH = 0.01;

        // Default bounds to evaluate over
        public const int XMIN = -3;
        public const int XMAX = 3;
        public const int YMIN = -3;
        public const int YMAX = 3;
        public const int ZMIN = -3;
        public const int ZMAX = 3;
    }

    public class PythonCode
    {
        public static double adapt(double v0, double v1)
        {
            // v0 and v1 are numbers of opposite sign. This returns how far you need to interpolate from v0 to v1 to get to 0.
            if ((v1 > 0) != (v0 > 0))
            {
                throw new ArgumentException("v0 and v1 do not have opposite sign");
            }

            if (Settings.ADAPTIVE)
                return (0 - v0) / (v1 - v0);
            else
                return 0.5;
        }

        // Provides a function for performing 3D Dual Countouring
        public Vector3 dual_contour_3d_find_best_vertex(Func<double, double, double, double> f, Vector3 f_normal, double x, double y, double z)
        {
            if (!Settings.ADAPTIVE)
            {
                return new Vector3(x + 0.5, y + 0.5, z + 0.5);
            }

            // # Evaluate f at each corner
            var v = new double[2, 2, 2];
            for (int dx = 0; dx <= 1; dx++)
                for (int dy = 0; dy <= 1; dy++)
                    for (int dz = 0; dz <= 1; dz++)
                        v[dx, dy, dz] = f(x + dx, y + dy, z + dz);

            // For each edge, identify where there is a sign change.
            // There are 4 edges along each of the three axes
            var changes = new List<Vector3>();
            for (int dx = 0; dx <= 1; dx++)
                for (int dy = 0; dy <= 1; dy++)
                    if ((v[dx, dy, 0] > 0) != (v[dx, dy, 1] > 0))
                        changes.Add(new Vector3(x + dx, y + dy, z + adapt(v[dx, dy, 0], v[dx, dy, 1])));

            for (int dx = 0; dx <= 1; dx++)
                for (int dz = 0; dz <= 1; dz++)
                    if ((v[dx, 0, dz] > 0) != (v[dx, 1, dz] > 0))
                        changes.Add(new Vector3(x + dx, y + adapt(v[dx, 0, dz], v[dx, 1, dz]), z + dz));

            for (int dy = 0; dy <= 1; dy++)
                for (int dz = 0; dz <= 1; dz++)
                    if ((v[0, dy, dz] > 0) != (v[1, dy, dz] > 0))
                        changes.Add(new Vector3(x + adapt(v[0, dy, dz], v[1, dy, dz]), y + dy, z + dz));

            if (changes.Count <= 1)
                return Vector3.PositiveInfinity;

            // For each sign change location v[i], we find the normal n[i].
            // The error term we are trying to minimize is sum( dot(x-v[i], n[i]) ^ 2)

            // In other words, minimize || A * x - b || ^2 where A and b are a matrix and vector
            // derived from v and n

            var normals = new List<Vector3>();

            foreach (var change in changes)
                normals.Add(change.GetNormal());

            return QEF.solve_qef_3d(x, y, z, changes, normals);
        }


        public Mesh dual_contour_3d(Func<double, double, double, double> f,
            Vector3 f_normal,
            int xmin = Settings.XMIN,
            int xmax = Settings.XMAX,
            int ymin = Settings.YMIN,
            int ymax = Settings.YMAX,
            int zmin = Settings.ZMIN,
            int zmax = Settings.ZMAX)
        {
            // Iterates over a cells of size one between the specified range, and evaluates f and f_normal to produce
            // a boundary by Dual Contouring.Returns a Mesh object.
            // For each cell, find the best vertex for fitting f
            var vert_array = new List<Vector3>();
            var vert_indices = new Dictionary<(int, int, int), int>();
            for (var x = xmin; x < xmax; x++)
                for (var y = ymin; y < ymax; y++)
                    for (var z = zmin; z < zmax; z++)
                    {
                        var vert = dual_contour_3d_find_best_vertex(f, f_normal, x, y, z);
                        if (vert == Vector3.NegativeInfinity)
                            continue;
                        vert_array.Add(vert);


                        vert_indices[(x, y, z)] = vert_array.Count;
                    }

            // For each cell edge, emit an face between the center of the adjacent cells if it is a sign changing edge
            var faces = new List<Quad>();
            for (var x = xmin; x < xmax; x++)
                for (var y = ymin; y < ymax; y++)
                    for (var z = zmin; z < zmax; z++)
                    {
                        if (x > xmin && y > ymin)
                        {
                            var solid1 = f(x, y, z + 0) > 0;
                            var solid2 = f(x, y, z + 1) > 0;
                            if (solid1 != solid2)
                                faces.Add((new Quad(
                                    vert_indices[(x - 1, y - 1, z)],
                                    vert_indices[(x - 0, y - 1, z)],
                                    vert_indices[(x - 0, y - 0, z)],
                                    vert_indices[(x - 1, y - 0, z)])).swap(solid2));
                        }
                        if (x > xmin && z > zmin)
                        {
                            var solid1 = f(x, y + 0, z) > 0;
                            var solid2 = f(x, y + 1, z) > 0;
                            if (solid1 != solid2)
                                faces.Add((new Quad(
                            vert_indices[(x - 1, y, z - 1)],
                            vert_indices[(x - 0, y, z - 1)],
                            vert_indices[(x - 0, y, z - 0)],
                            vert_indices[(x - 1, y, z - 0)])).swap(solid1));
                        }

                        if (y > ymin && z > zmin)
                        {
                            var solid1 = f(x + 0, y, z) > 0;
                            var solid2 = f(x + 1, y, z) > 0;
                            if (solid1 != solid2)
                                faces.Add((new Quad(
                                    vert_indices[(x, y - 1, z - 1)],
                                    vert_indices[(x, y - 0, z - 1)],
                            vert_indices[(x, y - 0, z - 0)],
                            vert_indices[(x, y - 1, z - 0)])).swap(solid2));
                        }
                    }

            return new Mesh(vert_array, faces);
        }


        public static double circle_function(double x, double y, double z)
        {
            return 2.5 - Math.Sqrt(x * x + y * y + z * z);
        }


        public static Vector3 circle_normal(double x, double y, double z)
        {
            var l = Math.Sqrt(x * x + y * y + z * z);
            return new Vector3(-x / l, -y / l, -z / l);
        }

        public static double intersect_function(double x, double y, double z)
        {
            y -= 0.3;
            x -= 0.5;
            x = Math.Abs(x);
            return Math.Min(x - y, x + y);
        }

        public static Func<double, double, double, Vector3> normal_from_function(Func<double, double, double, double> f, double d = 0.01)
        {
            // Given a sufficiently smooth 3d function, f, returns a function approximating of the gradient of f.
            // d controls the scale, smaller values are a more accurate approximation.
            Vector3 norm(double x, double y, double z)
            {
                return new Vector3(
                    (f(x + d, y, z) - f(x - d, y, z)) / 2 / d,
                    (f(x, y + d, z) - f(x, y - d, z)) / 2 / d,
                    (f(x, y, z + d) - f(x, y, z - d)) / 2 / d
                ).GetNormal();
            }

            return norm;
        }

        //mesh = dual_contour_3d(intersect_function, normal_from_function(intersect_function))
        //with open("output.obj", "w") as f:
        // make_obj(f, mesh)
    }

        public class QEF
		{
        // Represents and solves the quadratic error function
        public QEF(List<Vector3> normals, List<Vector3> positions, List<double> fixed_values)
        {
            this.A = normals;
            this.b = b;
            this.fixed_values = fixed_values;
        }

        public Vector3 evaluate(Vector3 x)
        {
            // Evaluates the function at a given point.
            // This is what the solve method is trying to minimize.
            // NB: Doesn't work with fixed axes.
            return numpy.linalg.norm(numpy.matmul(this.A, x) - this.b);
        }

        public (Vector3, Vector3) eval_with_pos(Vector3 x)
        {
            // Evaluates the QEF at a position, returning the same format solve does.
            return (this.evaluate(x), x);
        }

        public static Vector2 make_2d(List<Vector2> positions, List<Vector2> normals)
        {
            var b = new List<double>();
            // Returns a QEF that measures the the error from a bunch of normals, each emanating from given positions
            for (int i = 0; i < positions.Count; i++)
            {
                b.Add(Vector2.Dot(positions[i], normals[i]));
            }

            var fixed_values = [None] * A.shape[1];
            return new QEF(A, b, fixed_values);
        }

        public static Vector3 make_3d(List<Vector3> positions, List<Vector3> normals)
        {
            // Returns a QEF that measures the the error from a bunch of normals, each emanating from given positions
            var A = numpy.array(normals);
            var b = [v[0] * n[0] + v[1] * n[1] + v[2] * n[2] for v, n in zip(positions, normals)];
            var fixed_values = [None] * A.shape[1];
            return new QEF(A, b, fixed_values);
        }

        public QEF fix_axis(axis, value)
        {
            // Returns a new QEF that gives the same values as the old one, only with the position along the given axis
            // constrained to be value.
            // Pre-evaluate the fixed axis, adjusting b
            b = this.b[:] - this.A[:, axis] * value;
            // Remove that axis from a
            A = numpy.delete(this.A, axis, 1);
            fixed_values = this.fixed_values[:];
            fixed_values[axis] = value;
            return new QEF(A, b, fixed_values);
        }

        public (double, double, double, Vector3) solve()
        {
            // Finds the point that minimizes the error of this QEF,
            //and returns a tuple of the error squared and the point itself
            result, residual, rank, s = numpy.linalg.lstsq(this.A, this.b);
            if (len(residual) == 0)
                residual = this.evaluate(result);
            else
                residual = residual[0];
            // Result only contains the solution for the unfixed axis,
            // we need to add back all the ones we previously fixed.
            position = [];
            i = 0;
            foreach (value in this.fixed_values)
                if (value is None)
                {
                    position.append(result[i]);
                    i += 1;
                }
                else
                    position.append(value);
            return residual, position;
        }


def solve_qef_2d(x, y, positions, normals):
    // The error term we are trying to minimize is sum( dot(x-v[i], n[i]) ^ 2)
    // This should be minimized over the unit square with top left point (x, y)

    // In other words, minimize || A * x - b || ^2 where A and b are a matrix and vector
    // derived from v and n
    // The heavy lifting is done by the QEF class, but this function includes some important
    // tricks to cope with edge cases

    // This is demonstration code and isn't optimized, there are many good C++ implementations
    // out there if you need speed.

    if Settings.BIAS:
        // Add extra normals that add extra error the further we go
        // from the cell, this encourages the final result to be
        // inside the cell
        // These normals are shorter than the input normals
        // as that makes the bias weaker,  we want them to only
        // really be important when the input is ambiguous

        // Take a simple average of positions as the point we will
        // pull towards.
        mass_point = numpy.mean(positions, axis=0)

        normals.append([Settings.BIAS_STRENGTH, 0])
        positions.append(mass_point)
        normals.append([0, Settings.BIAS_STRENGTH])
        positions.append(mass_point)

    qef = QEF.make_2d(positions, normals)

    residual, v = qef.solve()

    if Settings.BOUNDARY:
        def inside(r):
            return x <= r[1][0] <= x + 1 and y <= r[1][1] <= y + 1

        // It's entirely possible that the best solution to the qef is not actually
        // inside the cell.
        if not inside((residual, v)):
            // If so, we constrain the the qef to the horizontal and vertical
            // lines bordering the cell, and find the best point of those
        r1 = qef.fix_axis(0, x + 0).solve()
            r2 = qef.fix_axis(0, x + 1).solve()
            r3 = qef.fix_axis(1, y + 0).solve()
            r4 = qef.fix_axis(1, y + 1).solve()

            rs = list(filter(inside, [r1, r2, r3, r4]))

            if len(rs) == 0:
                // It's still possible that those lines (which are infinite)
                // cause solutions outside the box. So finally, we evaluate which corner
                // of the cell looks best
                r1 = qef.eval_with_pos((x + 0, y + 0))
                r2 = qef.eval_with_pos((x + 0, y + 1))
                r3 = qef.eval_with_pos((x + 1, y + 0))
                r4 = qef.eval_with_pos((x + 1, y + 1))

                rs = list(filter(inside, [r1, r2, r3, r4]))

// Pick the best of the available options
        residual, v = min(rs)

    if Settings.CLIP:
        // Crudely force v to be inside the cell
        v[0] = numpy.clip(v[0], x, x + 1)
        v[1] = numpy.clip(v[1], y, y + 1)

    return V2(v[0], v[1])


public void solve_qef_3d(double x, double y, double z, List<Vector3> positions, List<Vector3> normals)
        {
            // The error term we are trying to minimize is sum( dot(x-v[i], n[i]) ^ 2)
            // This should be minimized over the unit square with top left point (x, y)

            // In other words, minimize || A * x - b || ^2 where A and b are a matrix and vector
            // derived from v and n
            // The heavy lifting is done by the QEF class, but this function includes some important
            // tricks to cope with edge cases

            // This is demonstration code and isn't optimized, there are many good C++ implementations
            // out there if you need speed.

            if (Settings.BIAS)
            {
                // Add extra normals that add extra error the further we go
                // from the cell, this encourages the final result to be
                // inside the cell
                // These normals are shorter than the input normals
                // as that makes the bias weaker,  we want them to only
                // really be important when the input is ambiguous

                // Take a simple average of positions as the point we will
                // pull towards.
                var mass_point = numpy.mean(positions, axis = 0);

                normals.append([Settings.BIAS_STRENGTH, 0, 0]);
                positions.append(mass_point);
                normals.append([0, Settings.BIAS_STRENGTH, 0]);
                positions.append(mass_point);
                normals.append([0, 0, Settings.BIAS_STRENGTH]);
                positions.append(mass_point);
            }

    var qef = QEF.make_3d(positions, normals)

    residual, v = qef.solve()

    if Settings.BOUNDARY:
        def inside(r):
            return x <= r[1][0] <= x + 1 and y <= r[1][1] <= y + 1 and z <= r[1][2] <= z + 1

        // It's entirely possible that the best solution to the qef is not actually
        // inside the cell.
            if not inside((residual, v)):
// If so, we constrain the the qef to the 6
// planes bordering the cell, and find the best point of those
            r1 = qef.fix_axis(0, x + 0).solve()
            r2 = qef.fix_axis(0, x + 1).solve()
            r3 = qef.fix_axis(1, y + 0).solve()
            r4 = qef.fix_axis(1, y + 1).solve()
            r5 = qef.fix_axis(2, z + 0).solve()
            r6 = qef.fix_axis(2, z + 1).solve()

            rs = list(filter(inside, [r1, r2, r3, r4, r5, r6]))

            if len(rs) == 0:
// It's still possible that those planes (which are infinite)
// cause solutions outside the box.
// So now try the 12 lines bordering the cell
                r1 = qef.fix_axis(1, y + 0).fix_axis(0, x + 0).solve()
                r2  = qef.fix_axis(1, y + 1).fix_axis(0, x + 0).solve()
                r3  = qef.fix_axis(1, y + 0).fix_axis(0, x + 1).solve()
                r4  = qef.fix_axis(1, y + 1).fix_axis(0, x + 1).solve()
                r5  = qef.fix_axis(2, z + 0).fix_axis(0, x + 0).solve()
                r6  = qef.fix_axis(2, z + 1).fix_axis(0, x + 0).solve()
                r7  = qef.fix_axis(2, z + 0).fix_axis(0, x + 1).solve()
                r8  = qef.fix_axis(2, z + 1).fix_axis(0, x + 1).solve()
                r9  = qef.fix_axis(2, z + 0).fix_axis(1, y + 0).solve()
                r10 = qef.fix_axis(2, z + 1).fix_axis(1, y + 0).solve()
                r11 = qef.fix_axis(2, z + 0).fix_axis(1, y + 1).solve()
                r12 = qef.fix_axis(2, z + 1).fix_axis(1, y + 1).solve()

                rs = list(filter(inside, [r1, r2, r3, r4, r5, r6, r7, r8, r9, r10, r11, r12]))

            if len(rs) == 0:
// So finally, we evaluate which corner
// of the cell looks best
                r1 = qef.eval_with_pos((x + 0, y + 0, z + 0))
                r2 = qef.eval_with_pos((x + 0, y + 0, z + 1))
                r3 = qef.eval_with_pos((x + 0, y + 1, z + 0))
                r4 = qef.eval_with_pos((x + 0, y + 1, z + 1))
                r5 = qef.eval_with_pos((x + 1, y + 0, z + 0))
                r6 = qef.eval_with_pos((x + 1, y + 0, z + 1))
                r7 = qef.eval_with_pos((x + 1, y + 1, z + 0))
                r8 = qef.eval_with_pos((x + 1, y + 1, z + 1))

                rs = list(filter(inside, [r1, r2, r3, r4, r5, r6, r7, r8]))

            // Pick the best of the available options
            residual, v = min(rs)

    if Settings.CLIP:
        // Crudely force v to be inside the cell
        v[0] = numpy.clip(v[0], x, x + 1)
        v[1] = numpy.clip(v[1], y, y + 1)
        v[2] = numpy.clip(v[2], z, z + 1)

    return V3(v[0], v[1], v[2])
        */
    }
#endif
}