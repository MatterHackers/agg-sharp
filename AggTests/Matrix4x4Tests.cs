/*
 * Created by SharpDevelop.
 * User: lbrubaker
 * Date: 3/26/2010
 * Time: 4:41 PM
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using NUnit.Framework;
using MatterHackers.Agg;
using Gaming.Math;

namespace MatterHackers.Agg.Tests
{
    [TestFixture]
	public class Matrix4x4Tests
    {
#if false
        System.Random TempRand = new Random();

        static bool TestOne(double Tx, double Ty, double Tz)
        {
            return TestOne(Tx, Ty, Tz, 0.0f, 0.0f, 0.0f, 1.0f, 1.0f, 1.0f);
        }

        static bool TestOne(double Tx, double Ty, double Tz, double Rx, double Ry, double Rz)
        {
            return TestOne(Tx, Ty, Tz, Rx, Ry, Rz, 1.0f, 1.0f, 1.0f);
        }

        static bool TestOne(double Tx, double Ty, double Tz, double Rx, double Ry, double Rz, double Sx, double Sy, double Sz)
        {
            Vector3 UnitVectorY = new Vector3(0.0f, 1.0f, 0.0f);
            Vector3 v1 = new Vector3();
	        v1 = UnitVectorY;
            Matrix4X4 NormalMatrix = Matrix4X4.Identity;
            Matrix4X4 InverseMatrixFromNormalMatrix = Matrix4X4.Identity;
            Matrix4X4 InverseMatrixCalculated = Matrix4X4.Identity;

	        NormalMatrix.PrepareMatrix(Tx, Ty, Tz, Rx, Ry, Rz, Sx, Sy, Sz);
	        NormalMatrix.TransformVector(ref v1);

            InverseMatrixFromNormalMatrix.SetToInverse(NormalMatrix);
            InverseMatrixFromNormalMatrix.TransformVector(ref v1);

	        // make sure they are the same within an error range
            Assert.IsTrue(v1.Equals(UnitVectorY, .01f));

            NormalMatrix.TransformVector(ref v1);

	        InverseMatrixCalculated.PrepareInvMatrix(-Tx, -Ty, -Tz, -Rx, -Ry, -Rz, 1.0f/Sx, 1.0f/Sy, 1.0f/Sz);
            InverseMatrixCalculated.TransformVector(ref v1);

	        // make sure they are the same within an error range
            Assert.IsTrue(v1.Equals(UnitVectorY, .001f));

	        // And just a bit more checking [7/26/2001] LBB
	        // and now just check that TransformVector is always working
	        NormalMatrix.PrepareMatrix(Tx, Ty, Tz, Rx, Ry, Rz, Sx, Sy, Sz);
            NormalMatrix.TransformVector3X3(ref v1);
	        InverseMatrixCalculated.PrepareInvMatrix(-Tx, -Ty, -Tz, -Rx, -Ry, -Rz, 1.0f/Sx, 1.0f/Sy, 1.0f/Sz);
            InverseMatrixCalculated.TransformVector3X3(ref v1);
            Assert.IsTrue(v1.Equals(UnitVectorY, .001f));

	        NormalMatrix.PrepareMatrix(Tx, Ty, Tz, Rx, Ry, Rz, Sx, Sy, Sz);
            NormalMatrix.TransformVector3X3(ref v1);
            InverseMatrixCalculated.SetToInverse(NormalMatrix);
            InverseMatrixCalculated.TransformVector3X3(ref v1);
            Assert.IsTrue(v1.Equals(UnitVectorY, .001f));

	        return true;
        }

        public double RandomDouble(System.Random Rand, double Min, double Max)
        {
            return (double)Rand.NextDouble() * (Max - Min) + Min;
        }

        [Test]
        public void MatrixColumnMajor()
        {
            // Make sure our matrix is set up colum major like opengl. LBB [7/11/2003]
            Matrix4X4 ColumnMajorRotationMatrix = Matrix4X4.CreateRotationY(.2345f);
            Matrix4X4 ColumnMajorTransLationMatrix = Matrix4X4.Identity;
            ColumnMajorTransLationMatrix.Translate(.2342f, 234234.734f, 223.324f);
            Matrix4X4 ColumnMajorAccumulatedMatrix = Matrix4X4.Identity;
            ColumnMajorAccumulatedMatrix = ColumnMajorRotationMatrix * ColumnMajorTransLationMatrix;
            double[] KnownMatrixFormFloats = 
	        {
		        .972631f,	0.0f,		-.232357f, 0.0f, 
		        0.0f,		1.0f,		0.0f,		0.0f,
		        .232357f,	0.0f,		.972631f,	0.0f, 
		        .2342f,		234234.73f,	223.324f,	1.0f 
	        };
            Matrix4X4 KnownMatrixForm = Matrix4X4.Identity;
            KnownMatrixForm.SetElements(KnownMatrixFormFloats);
            Assert.IsTrue(KnownMatrixForm.Equals(ColumnMajorAccumulatedMatrix, .01f));
        }

        [Test]
        public void RotateAboutXAxis()
        {
            Vector3 RotateAboutX = new Vector3(1.0f, 0.0f, 0.0f);
            Matrix4X4 RotationMatrix = Matrix4X4.Identity;
            RotationMatrix.Rotate(RotateAboutX, (double)(System.Math.PI / 2));
            Vector3 PointToRotate = new Vector3(0, 40, 0);
            RotationMatrix.TransformVector(ref PointToRotate);
            Assert.IsTrue(PointToRotate.Equals(new Vector3(0, 0, 40), .01f));
        }
        [Test]
        public void RotateAboutYAxis()
        {
            Vector3 RotateAboutY = new Vector3(0.0f, 1.0f, 0.0f);
            Matrix4X4 RotationMatrix = Matrix4X4.Identity;
            RotationMatrix.Rotate(RotateAboutY, (double)(System.Math.PI / 2));
            Vector3 PointToRotate = new Vector3(40, 0, 0);
            RotationMatrix.TransformVector(ref PointToRotate);
            Assert.IsTrue(PointToRotate.Equals(new Vector3(0, 0, -40), .01f));
        }
        [Test]
        public void RotateAboutZAxis()
        {
            Vector3 RotateAboutZ = new Vector3(0.0f, 0.0f, 1.0f);
            Matrix4X4 RotationMatrix = Matrix4X4.Identity;
            RotationMatrix.Rotate(RotateAboutZ, (double)(System.Math.PI / 2));
            Vector3 PointToRotate = new Vector3(40, 0, 0);
            RotationMatrix.TransformVector(ref PointToRotate);
            Assert.IsTrue(PointToRotate.Equals(new Vector3(0, 40, 0), .01f));
        }

        [Test]
        public void ConcatenatedMatrixIsSameAsIndividualMatrices ()
        {
            // Make sure that pushing a concatenated matrix is the same as through a bunch of individual matrices [7/30/2001] LBB
            uint NumTransforms = (uint)TempRand.Next(10) + 4;
            Vector3 UnitVectorY = new Vector3(0.0f, 1.0f, 0.0f);
            Vector3 EachMatrixVector = new Vector3();
            Vector3 ConcatenatedMatrixVector = new Vector3();
            EachMatrixVector = UnitVectorY;
            Matrix4X4 ConcatenatedMatrix = Matrix4X4.Identity;
            ConcatenatedMatrix.Identity();
            Matrix4X4[] pTranforms = new Matrix4X4[NumTransforms];
            for (int i = 0; i < NumTransforms; i++)
            {
                pTranforms[i] = Matrix4X4.Identity;
            }

            for (uint CurTransform = 0; CurTransform < NumTransforms; CurTransform++)
            {
                uint Axis = (uint)TempRand.Next(3);
                double Rotation = RandomDouble(TempRand, 0.01f, 2 * (double)System.Math.PI);
                Vector3 Translation;
                Translation.x = RandomDouble(TempRand, -10000.0f, 10000.0f);
                Translation.y = RandomDouble(TempRand, -10000.0f, 10000.0f);
                Translation.z = RandomDouble(TempRand, -10000.0f, 10000.0f);
                if (TempRand.Next(2) != 0)
                {
                    pTranforms[CurTransform].Rotate(Axis, Rotation);
                }
                else
                {
                    pTranforms[CurTransform].Translate(Translation);
                }

                pTranforms[CurTransform].TransformVector(ref EachMatrixVector);
                ConcatenatedMatrix.Multiply(pTranforms[CurTransform]); // this is working for rotation
                ConcatenatedMatrixVector = UnitVectorY;
                ConcatenatedMatrix.TransformVector(ref ConcatenatedMatrixVector);
                Assert.IsTrue(ConcatenatedMatrixVector.Equals(EachMatrixVector, .01f));
            }
        }

        [Test]
        public void PrepareAsInveresAndInverseAreSame()
        {
            //***************************************
            TestOne(0.0f, 0.0f, 0.0f, (double)System.Math.PI / 2, 0, 0);
            TestOne(0.0f, 0.0f, 0.0f, 0, (double)System.Math.PI / 2, 0);
            TestOne(0.0f, 0.0f, 0.0f, 0, 0, (double)System.Math.PI / 2);

            //***************************************
            TestOne(5.0f, 50.0f, 10.0f);

            //***************************************
            TestOne(5.0f, 50.0f, 10.0f, 3.0f, 2.0f, 33333.0f);

            // Let's just sling a bunch of test [7/26/2001] LBB
            TestOne(10.0f, 0.0f, 0.0f, 0.0f, 0.0f, (double)System.Math.PI / 2.0f);
            TestOne(0.0f, 10.0f, 0.0f, 0.0f, 0.0f, (double)System.Math.PI / 2.0f);
            TestOne(0.0f, 0.0f, 10.0f, 0.0f, 0.0f, (double)System.Math.PI / 2.0f);

            TestOne(10.0f, 0.0f, 0.0f, 0.0f, (double)System.Math.PI / 2.0f, 0.0f);
            TestOne(0.0f, 10.0f, 0.0f, 0.0f, (double)System.Math.PI / 2.0f, 0.0f);
            TestOne(0.0f, 0.0f, 10.0f, 0.0f, (double)System.Math.PI / 2.0f, 0.0f);

            TestOne(10.0f, 0.0f, 0.0f, (double)System.Math.PI / 2.0f, 0.0f, 0.0f);
            TestOne(0.0f, 10.0f, 0.0f, (double)System.Math.PI / 2.0f, 0.0f, 0.0f);
            TestOne(0.0f, 0.0f, 10.0f, (double)System.Math.PI / 2.0f, 0.0f, 0.0f);

            for (uint i = 0; i < 100; i++)
            {
                TestOne(
                    RandomDouble(TempRand, -1000.0f, 1000.0f),
                    RandomDouble(TempRand, -1000.0f, 1000.0f),
                    RandomDouble(TempRand, -1000.0f, 1000.0f),

                    RandomDouble(TempRand, -2.0f * (double)System.Math.PI, 2.0f * (double)System.Math.PI),
                    RandomDouble(TempRand, -2.0f * (double)System.Math.PI, 2.0f * (double)System.Math.PI),
                    RandomDouble(TempRand, -2.0f * (double)System.Math.PI, 2.0f * (double)System.Math.PI),

                    RandomDouble(TempRand, 0.001f, 1000.0f),
                    RandomDouble(TempRand, 0.001f, 1000.0f),
                    RandomDouble(TempRand, 0.001f, 1000.0f));
            }
        }

        [Test]
        public void PrepareMatrixFromPositionAndDirection()
        {
	        // Test the PrepareMatrixFromPositionAndDirection function.
            Matrix4X4 TestA = Matrix4X4.Identity;
            TestA.PrepareMatrixFromPositionAndDirection(new Vector3(1.0f, 2.0f, 3.0f), new Vector3(1.0f, 0.0f, 1.0f));
	        double[] TestACorrectResultFloats = 
	        {
		        .7073f,		0.0f,		-.7073f,	0.0f, 
		        .7072f,		0.0f,		.7072f,		0.0f,
		        0.0f,		-1.0f,		0.0f,		0.0f, 
		        1.0f,		2.0f,		3.0f,		1.0f 
	        };
            Matrix4X4 TestACorrectResult = Matrix4X4.Identity;
	        TestACorrectResult.SetElements(TestACorrectResultFloats);
	        Assert.IsTrue(TestACorrectResult.Equals(TestA, .01f));

            Matrix4X4 TestB = Matrix4X4.Identity;
            TestB.PrepareMatrixFromPositionAndDirection(new Vector3(1.0f, 2.0f, 3.0f), new Vector3(1.0f, 0.0f, 0.01f));
	        double[] TestBCorrectResultFloats = 
	        {
		        0.0f,		1.0f,		0.0f,		0.0f, 
		        1.0f,		0.0f,		.0099f,		0.0f,
		        0.0099f,	0.0f,		-1.0f,		0.0f, 
		        1.0f,		2.0f,		3.0f,		1.0f 
	        };
            Matrix4X4 TestBCorrectResult = Matrix4X4.Identity;
	        TestBCorrectResult.SetElements(TestBCorrectResultFloats);
	        Assert.IsTrue(TestBCorrectResult.Equals(TestB, .01f));
        }
#endif
    }
}
