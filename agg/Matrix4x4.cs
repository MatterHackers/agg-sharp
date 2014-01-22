using System;
using System.Collections.Generic;
using System.Text;

namespace AGG
{
    public class Matrix4X4
    {
        double[] matrix = new double[16];

        public Matrix4X4()
        {
            SetElement(0, 0, 1.0f);
            SetElement(1, 1, 1.0f);
            SetElement(2, 2, 1.0f);
            SetElement(3, 3, 1.0f);
        }

        public Matrix4X4(Matrix4X4 CopyFrom)
        {
            for(int i=0; i<16; i++)
            {
                matrix[i] = CopyFrom.matrix[i];
            }
        }

        public Matrix4X4(double[] CopyFrom)
        {
            SetElements(CopyFrom);
        }

        public double this[int index]
        {
            get
            {
                return matrix[index];
            }

            set
            {
                matrix[index] = value;
            }
        }

        public double this[int row, int column]
        {
            get
            {
                return GetElement(row, column);
            }

            set
            {
                SetElement(row, column, value);
            }
        }

        public double GetElement(int Row, int Column) 
        {
            return matrix[Row * 4 + Column]; 
        }

        public void SetElement(int Row, int Column, double Value)
        {
            matrix[Row * 4 + Column] = Value; 
        }

        public void AddElement(int Row, int Column, double Value) 
        {
            matrix[Row * 4 + Column] += Value; 
        }

        public void Identity()
        {
	        Zero();
	        SetElement(0, 0, 1.0f);
	        SetElement(1, 1, 1.0f);
	        SetElement(2, 2, 1.0f);
	        SetElement(3, 3, 1.0f);
        }

        public void Zero()
        {
            for (int i = 0; i < 16; i++)
            {
                matrix[i] = 0;
            }
        }

        // A bit of code from Intel LBB [10/29/2003]
        /************************************************************
        *
        * input:
        * mat - pointer to array of 16 doubles (source matrix)
        * output:
        * dst - pointer to array of 16 doubles (invert matrix)
        *
        Version														Cycles
        C code with Cramer's rule									846
        C code with Cramer's rule & Streaming SIMD Extensions		210
        *************************************************************/
        static void IntelInvertC(double[] matrixToInvert, double[] destMatrix)
        {
            double[] tmp = new double[12]; /* temp array for pairs */
            double[] src = new double[16]; /* array of transpose source matrix */
            double det; /* determinant */
	        /* transpose matrix */
	        for ( int i = 0; i < 4; i++) {
		        src[i] = matrixToInvert[i*4];
		        src[i + 4] = matrixToInvert[i*4 + 1];
		        src[i + 8] = matrixToInvert[i*4 + 2];
		        src[i + 12] = matrixToInvert[i*4 + 3];
	        }
	        /* calculate pairs for first 8 elements (cofactors) */
	        tmp[0] = src[10] * src[15];
	        tmp[1] = src[11] * src[14];
	        tmp[2] = src[9] * src[15];
	        tmp[3] = src[11] * src[13];
	        tmp[4] = src[9] * src[14];
	        tmp[5] = src[10] * src[13];
	        tmp[6] = src[8] * src[15];
	        tmp[7] = src[11] * src[12];
	        tmp[8] = src[8] * src[14];
	        tmp[9] = src[10] * src[12];
	        tmp[10] = src[8] * src[13];
	        tmp[11] = src[9] * src[12];
	        /* calculate first 8 elements (cofactors) */
	        destMatrix[0] = tmp[0]*src[5] + tmp[3]*src[6] + tmp[4]*src[7];
	        destMatrix[0] -= tmp[1]*src[5] + tmp[2]*src[6] + tmp[5]*src[7];
	        destMatrix[1] = tmp[1]*src[4] + tmp[6]*src[6] + tmp[9]*src[7];
	        destMatrix[1] -= tmp[0]*src[4] + tmp[7]*src[6] + tmp[8]*src[7];
	        destMatrix[2] = tmp[2]*src[4] + tmp[7]*src[5] + tmp[10]*src[7];
	        destMatrix[2] -= tmp[3]*src[4] + tmp[6]*src[5] + tmp[11]*src[7];
	        destMatrix[3] = tmp[5]*src[4] + tmp[8]*src[5] + tmp[11]*src[6];
	        destMatrix[3] -= tmp[4]*src[4] + tmp[9]*src[5] + tmp[10]*src[6];
	        destMatrix[4] = tmp[1]*src[1] + tmp[2]*src[2] + tmp[5]*src[3];
	        destMatrix[4] -= tmp[0]*src[1] + tmp[3]*src[2] + tmp[4]*src[3];
	        destMatrix[5] = tmp[0]*src[0] + tmp[7]*src[2] + tmp[8]*src[3];
	        destMatrix[5] -= tmp[1]*src[0] + tmp[6]*src[2] + tmp[9]*src[3];
	        destMatrix[6] = tmp[3]*src[0] + tmp[6]*src[1] + tmp[11]*src[3];
	        destMatrix[6] -= tmp[2]*src[0] + tmp[7]*src[1] + tmp[10]*src[3];
	        destMatrix[7] = tmp[4]*src[0] + tmp[9]*src[1] + tmp[10]*src[2];
	        destMatrix[7] -= tmp[5]*src[0] + tmp[8]*src[1] + tmp[11]*src[2];
	        /* calculate pairs for second 8 elements (cofactors) */
	        tmp[0] = src[2]*src[7];
	        tmp[1] = src[3]*src[6];
	        tmp[2] = src[1]*src[7];
	        tmp[3] = src[3]*src[5];
	        tmp[4] = src[1]*src[6];
	        tmp[5] = src[2]*src[5];
	        tmp[6] = src[0]*src[7];
	        tmp[7] = src[3]*src[4];
	        tmp[8] = src[0]*src[6];
	        tmp[9] = src[2]*src[4];
	        tmp[10] = src[0]*src[5];
	        tmp[11] = src[1]*src[4];
	        /* calculate second 8 elements (cofactors) */
	        destMatrix[8] = tmp[0]*src[13] + tmp[3]*src[14] + tmp[4]*src[15];
	        destMatrix[8] -= tmp[1]*src[13] + tmp[2]*src[14] + tmp[5]*src[15];
	        destMatrix[9] = tmp[1]*src[12] + tmp[6]*src[14] + tmp[9]*src[15];
	        destMatrix[9] -= tmp[0]*src[12] + tmp[7]*src[14] + tmp[8]*src[15];
	        destMatrix[10] = tmp[2]*src[12] + tmp[7]*src[13] + tmp[10]*src[15];
	        destMatrix[10]-= tmp[3]*src[12] + tmp[6]*src[13] + tmp[11]*src[15];
	        destMatrix[11] = tmp[5]*src[12] + tmp[8]*src[13] + tmp[11]*src[14];
	        destMatrix[11]-= tmp[4]*src[12] + tmp[9]*src[13] + tmp[10]*src[14];
	        destMatrix[12] = tmp[2]*src[10] + tmp[5]*src[11] + tmp[1]*src[9];
	        destMatrix[12]-= tmp[4]*src[11] + tmp[0]*src[9] + tmp[3]*src[10];
	        destMatrix[13] = tmp[8]*src[11] + tmp[0]*src[8] + tmp[7]*src[10];
	        destMatrix[13]-= tmp[6]*src[10] + tmp[9]*src[11] + tmp[1]*src[8];
	        destMatrix[14] = tmp[6]*src[9] + tmp[11]*src[11] + tmp[3]*src[8];
	        destMatrix[14]-= tmp[10]*src[11] + tmp[2]*src[8] + tmp[7]*src[9];
	        destMatrix[15] = tmp[10]*src[10] + tmp[4]*src[8] + tmp[9]*src[9];
	        destMatrix[15]-= tmp[8]*src[9] + tmp[11]*src[10] + tmp[5]*src[8];
	        /* calculate determinant */
	        det=src[0]*destMatrix[0]+src[1]*destMatrix[1]+src[2]*destMatrix[2]+src[3]*destMatrix[3];
	        /* calculate matrix inverse */
	        det = 1/det;
	        for ( int j = 0; j < 16; j++)
            {
		        destMatrix[j] *= det;
            }  
        }

        public bool SetToInverse(Matrix4X4 OriginalMatrix)
        {
	        IntelInvertC(OriginalMatrix.matrix, matrix);
	        return true;
        }

        public bool Invert()
        {
	        Matrix4X4 Temp = new Matrix4X4(this);
	        return SetToInverse(Temp);
        }

        private void matrix_swap_mirror(int a, int b)
        {
            double Temp = GetElement(a, b);
            SetElement(a,b, GetElement(b,a));
            SetElement(b, a, Temp);
        }

        public void Transpose3X3()
        {
	        matrix_swap_mirror(0,1);
	        matrix_swap_mirror(0,2);
	        matrix_swap_mirror(0,3);
	        matrix_swap_mirror(1,2);
	        matrix_swap_mirror(1,3);
	        matrix_swap_mirror(2,3);
        }

        public Vector3 Position
        {
            get
            {
                return new Vector3(GetElement(3, 0), GetElement(3, 1), GetElement(3, 2));
            }

            set
            {
                SetElement(3, 0, value.x);
                SetElement(3, 1, value.y);
                SetElement(3, 2, value.z);
            }
        }

        public void Translate(double tx, double ty, double tz)
        {
	        int i;

	        Zero();
	        for(i=0; i<4; i++)
	        {
		        SetElement(i, i, 1.0f);
	        }

            // <Simon 2002/05/02> fixed matrix ordering problem
	        SetElement(3, 0, tx);
	        SetElement(3, 1, ty);
	        SetElement(3, 2, tz);
        }

        public void Translate(Vector3 Vect)
        {
	        Translate(Vect.x, Vect.y, Vect.z);
        }

        public void AddTranslate(double x, double y, double z)
        {
            AddTranslate(new Vector3(x, y, z));
        }

        public void AddTranslate(Vector3 Vect)
        {
            Matrix4X4 Temp = new Matrix4X4();
            Temp.Translate(Vect.x, Vect.y, Vect.z);

            Multiply(Temp);
        }

        public void Scale(float sx, float sy, float sz)
        {
            Scale((double)sx, (double)sy, (double)sz);
        }

        public void Scale(double sx, double sy, double sz)
        {
          Zero();
          SetElement(0, 0, sx);
          SetElement(1, 1, sy);
          SetElement(2, 2, sz);
          SetElement(3, 3, 1.0f);
        }

        public void AddRotate(uint Axis, double Theta)
        {
            Matrix4X4 Temp = new Matrix4X4();
            Temp.Rotate(Axis, Theta);

            Multiply(Temp);
        }

        public void Rotate(uint Axis, double Theta)
        {
            double c, s;

	        if(Theta != 0)
	        {
                c = (double)System.Math.Cos(Theta);
                s = (double)System.Math.Sin(Theta);
	        }
	        else
	        {
		        c = 1.0f;
		        s = 0.0f;
	        }

	        switch(Axis)
	        {
	        case 0:
		        SetElement(0, 0, 1.0f);
		        SetElement(0, 1, 0.0f);
		        SetElement(0, 2, 0.0f);
		        SetElement(0, 3, 0.0f);

		        SetElement(1, 0, 0.0f);
		        SetElement(1, 1, c);
		        SetElement(1, 2, s);
		        SetElement(1, 3, 0.0f);

		        SetElement(2, 0, 0.0f);
		        SetElement(2, 1, -s);
		        SetElement(2, 2, c);
		        SetElement(2, 3, 0.0f);
		        break;

	        case 1:
		        SetElement(0, 0, c);
		        SetElement(0, 1, 0.0f);
		        SetElement(0, 2, -s);
		        SetElement(0, 3, 0.0f);

		        SetElement(1, 0, 0.0f);
		        SetElement(1, 1, 1.0f);
		        SetElement(1, 2, 0.0f);
		        SetElement(1, 3, 0.0f);

		        SetElement(2, 0, s);
		        SetElement(2, 1, 0.0f);
		        SetElement(2, 2, c);
		        SetElement(2, 3, 0.0f);
		        break;

	        case 2:
		        SetElement(0, 0, c);
		        SetElement(0, 1, s);
		        SetElement(0, 2, 0.0f);
		        SetElement(0, 3, 0.0f);

		        SetElement(1, 0, -s);
		        SetElement(1, 1, c);
		        SetElement(1, 2, 0.0f);
		        SetElement(1, 3, 0.0f);

		        SetElement(2, 0, 0.0f);
		        SetElement(2, 1, 0.0f);
		        SetElement(2, 2, 1.0f);
		        SetElement(2, 3, 0.0f);
                break;
	        }

	        // set the ones that don't change
	        SetElement(3, 0, 0.0f);
	        SetElement(3, 1, 0.0f);
	        SetElement(3, 2, 0.0f);
	        SetElement(3, 3, 1.0f);
        }

        public void Rotate(Vector3 Axis, double AngleRadians)
        {
            Axis.Normalize();

            double Cos = (double)System.Math.Cos(AngleRadians);
            double Sin = (double)System.Math.Sin(AngleRadians);

            double OneMinusCos = 1.0f - Cos;

            matrix[0 + 4 * 0] = OneMinusCos * Axis.x * Axis.x + Cos;
            matrix[0 + 4 * 1] = OneMinusCos * Axis.x * Axis.y - Sin * Axis.z;
            matrix[0 + 4 * 2] = OneMinusCos * Axis.x * Axis.z + Sin * Axis.y;
            matrix[0 + 4 * 3] = 0.0f;

            matrix[1 + 4 * 0] = OneMinusCos * Axis.x * Axis.y + Sin * Axis.z;
            matrix[1 + 4 * 1] = OneMinusCos * Axis.y * Axis.y + Cos;
            matrix[1 + 4 * 2] = OneMinusCos * Axis.y * Axis.z - Sin * Axis.x;
            matrix[1 + 4 * 3] = 0.0f;

            matrix[2 + 4 * 0] = OneMinusCos * Axis.x * Axis.z - Sin * Axis.y;
            matrix[2 + 4 * 1] = OneMinusCos * Axis.y * Axis.z + Sin * Axis.x;
            matrix[2 + 4 * 2] = OneMinusCos * Axis.z * Axis.z + Cos;
            matrix[2 + 4 * 3] = 0.0f;

            matrix[3 + 4 * 0] = 0.0f;
            matrix[3 + 4 * 1] = 0.0f;
            matrix[3 + 4 * 2] = 0.0f;
            matrix[3 + 4 * 3] = 1.0f;
        }

        public bool Equals(Matrix4X4 OtherMatrix, double ErrorRange)
        {
	        for(int i=0; i<4; i++)
	        {
		        for(int j=0; j<4; j++)
		        {
			        if(		GetElement(i, j) < OtherMatrix.GetElement(i, j) - ErrorRange
				        ||	GetElement(i, j) > OtherMatrix.GetElement(i, j) + ErrorRange)
			        {
				        return false;
			        }
		        }
	        }

	        return true;
        }

        public void PrepareMatrix(double Tx, double Ty, double Tz,
                                       double Rx, double Ry, double Rz,
                                       double Sx, double Sy, double Sz)
        {
            bool Initialized = false;

            if (Sx != 1.0f || Sy != 1.0f || Sz != 1.0f)
            {
                if (Initialized)
                {
                    Matrix4X4 Temp = new Matrix4X4();
                    Temp.Scale(Sx, Sy, Sz);
                    Multiply(Temp);
                }
                else
                {
                    Scale(Sx, Sy, Sz);
                    Initialized = true;
                }
            }
            if (Rx != .0f)
            {
                if (Initialized)
                {
                    Matrix4X4 Temp = new Matrix4X4();
                    Temp.Rotate(0, Rx);
                    Multiply(Temp);
                }
                else
                {
                    Rotate(0, Rx);
                    Initialized = true;
                }
            }
            if (Ry != .0f)
            {
                if (Initialized)
                {
                    Matrix4X4 Temp = new Matrix4X4();
                    Temp.Rotate(1, Ry);
                    Multiply(Temp);
                }
                else
                {
                    Rotate(1, Ry);
                    Initialized = true;
                }
            }
            if (Rz != .0f)
            {
                if (Initialized)
                {
                    Matrix4X4 Temp = new Matrix4X4();
                    Temp.Rotate(2, Rz);
                    Multiply(Temp);
                }
                else
                {
                    Rotate(2, Rz);
                    Initialized = true;
                }
            }
            if (Tx != 0.0f || Ty != 0.0f || Tz != 0.0f)
            {
                if (Initialized)
                {
                    Matrix4X4 Temp = new Matrix4X4();
                    Temp.Translate(Tx, Ty, Tz);
                    Multiply(Temp);
                }
                else
                {
                    Translate(Tx, Ty, Tz);
                    Initialized = true;
                }

                if (!Initialized)
                {
                    Identity();
                }
            }
        }

        public void PrepareMatrix(Vector3 pTranslateVector,
							           Vector3 pRotateVector,
							           Vector3 pScaleVector)
        {
            PrepareMatrix(pTranslateVector.x, pTranslateVector.y, pTranslateVector.z,
		        pRotateVector.x, pRotateVector.y, pRotateVector.z,
		        pScaleVector.x, pScaleVector.y, pScaleVector.z);
        }

        public void PrepareMatrixFromPositionAndDirection(Vector3 Position, Vector3 Direction)
        {
	        // Setup translation part.
	        Translate(Position);

	        // Do orientation.
	        Vector3 YAxis = Direction;
	        YAxis.Normalize();

	        // Generate a candidate for the x axis.

	        // Try the world x axis first.
	        Vector3 XAxis = new Vector3(1.0f, 0.0f, 0.0f);

            double Threshold = (double)System.Math.Cos(10.0f * MathHelper.Tau / 360);

	        if (Vector3.Dot(YAxis, XAxis) > Threshold)
	        {
		        // Too close so use the world y axis.
		        XAxis = new Vector3(0.0f, 1.0f, 0.0f);
	        }

	        // Get the z axis from the cross product.
	        Vector3 ZAxis = Vector3.Cross(XAxis, YAxis);
	        ZAxis.Normalize();

	        // Get the true x axis from y and z.
	        XAxis =  Vector3.Cross(YAxis, ZAxis);

	        for (int i=0; i<3; i++)
	        {
		        SetElement(0, i, XAxis[i]);
		        SetElement(1, i, YAxis[i]);
		        SetElement(2, i, ZAxis[i]);
	        }
        	
	        SetElement(0, 3, 0.0f);
	        SetElement(1, 3, 0.0f);
	        SetElement(2, 3, 0.0f);

        }

        public void PrepareInvMatrix(double Tx, double Ty, double Tz,
                                          double Rx, double Ry, double Rz,
                                          double Sx, double Sy, double Sz)
        {
	        Matrix4X4 M0 = new Matrix4X4();
            Matrix4X4 M1 = new Matrix4X4();
            Matrix4X4 M2 = new Matrix4X4();
            Matrix4X4 M3 = new Matrix4X4();
            Matrix4X4 M4 = new Matrix4X4();
            Matrix4X4 M5 = new Matrix4X4();
            Matrix4X4 M6 = new Matrix4X4();
            Matrix4X4 M7 = new Matrix4X4();

	        M0.Scale(Sx, Sy, Sz);
	        M1.Rotate(0, Rx);
	        M2.Rotate(1, Ry);
	        M3.Rotate(2, Rz);
	        M4.Translate(Tx, Ty, Tz);
	        // 4 * 3 * 2 * 1 * 0
	        M5.Multiply(M4, M3);
	        M6.Multiply(M5, M2);
	        M7.Multiply(M6, M1);
	        Multiply(M7, M0);
        }

        public void PrepareInvMatrix(Vector3 pTranslateVector,
							           Vector3 pRotateVector,
							           Vector3 pScaleVector)
        {
            PrepareInvMatrix(pTranslateVector.x, pTranslateVector.y, pTranslateVector.z,
					         pRotateVector.x, pRotateVector.y, pRotateVector.z,
					         pScaleVector.x, pScaleVector.y, pScaleVector.z);
        }

        public void TransformVector(double[] pChanged)
        {
            double[] Hold = (double[])pChanged.Clone();
            pChanged[0] = GetElement(0, 0) * Hold[0] + GetElement(0, 1) * Hold[1] + GetElement(0, 2) * Hold[2] + GetElement(0, 3) * Hold[3];
            pChanged[1] = GetElement(1, 0) * Hold[0] + GetElement(1, 1) * Hold[1] + GetElement(1, 2) * Hold[2] + GetElement(1, 3) * Hold[3];
            pChanged[2] = GetElement(2, 0) * Hold[0] + GetElement(2, 1) * Hold[1] + GetElement(2, 2) * Hold[2] + GetElement(2, 3) * Hold[3];
            pChanged[3] = GetElement(3, 0) * Hold[0] + GetElement(3, 1) * Hold[1] + GetElement(3, 2) * Hold[2] + GetElement(3, 3) * Hold[3];
        }

        public void TransformVector(ref Vector3 Changed)
        {
	        Vector3 Original = Changed;
	        TransformVector(out Changed, Original);
        }

        public void TransformVector(out Vector3 Changed, Vector3 Original)
        {
	        TransformVector3X3(out Changed, Original);
            Changed.x += GetElement(3, 0);
            Changed.y += GetElement(3, 1);
            Changed.z += GetElement(3, 2);
        }

        public void TransformVector3X3(ref Vector3 Changed)
        {
	        Vector3 Original = new Vector3(Changed);
	        TransformVector3X3(out Changed, Original);
        }

        public void TransformVector3X3(out Vector3 Changed, Vector3 Original)
        {
            Changed.x = GetElement(0, 0) * Original.x + GetElement(1, 0) * Original.y + GetElement(2, 0) * Original.z;
            Changed.y = GetElement(0, 1) * Original.x + GetElement(1, 1) * Original.y + GetElement(2, 1) * Original.z;
            Changed.z = GetElement(0, 2) * Original.x + GetElement(1, 2) * Original.y + GetElement(2, 2) * Original.z;
        }

        public void TransformVector3X3(Vector3[] vertsToTransform)
        {
            for (int i = 0; i < vertsToTransform.Length; i++)
            {
                TransformVector3X3(ref vertsToTransform[i]);
            }
        }

        public uint ValidateMatrix()
        {
	        if( GetElement(3, 0) == 0.0f && GetElement(3, 1) == 0.0f && GetElement(3, 2) == 0.0f && GetElement(3, 3) == 1.0f)
	        {
		        return 1;
	        }

	        return 0;
        }

        public static Matrix4X4 operator *(Matrix4X4 A, Matrix4X4 B)
        {
            Matrix4X4 Temp = new Matrix4X4(A);
            Temp.Multiply(B);
            return Temp;
        }

        public void Multiply(Matrix4X4 Two)
        {
	        Matrix4X4 Hold = new Matrix4X4(this);
	        Multiply(Hold, Two);
        }

        public void Multiply(Matrix4X4 One, Matrix4X4 Two)
        {
            if (this == One || this == Two)
            {
                throw new System.FormatException("Neither of the input parameters can be the same Matrix as this.");
            }

	        for(int i = 0; i < 4; i++)
	        {
		        for(int j = 0; j < 4; j++)
		        {
                    SetElement(i, j, 0);
			        for(int k = 0; k < 4; k++)
			        {
				        AddElement(i, j, One.GetElement(i, k) * Two.GetElement(k, j));
			        }
		        }
	        }
        }

        // Returns the X-axis vector from this matrix
        public void GetXAxisVector (Vector3 result)
        {
	        // stored as row vectors
	        result.x = GetElement(0, 0);
            result.y = GetElement(0, 1);
            result.z = GetElement(0, 2);
        }

        // Returns the Y-axis vector from this matrix
        public void GetYAxisVector (Vector3 result)
        {
	        // stored as row vectors
            result.x = GetElement(1, 0);
            result.y = GetElement(1, 1);
            result.z = GetElement(1, 2);
        }

        // Returns the Z-axis vector from this matrix
        public void GetZAxisVector (Vector3 result)
        {
	        // stored as row vectors
            result.x = GetElement(2, 0);
            result.y = GetElement(2, 1);
            result.z = GetElement(2, 2);
        }

        // Returns the translation from this matrix
        public void GetTranslation (Vector3 result)
        {
	        // stored as row vectors
            result.x = GetElement(3, 0);
            result.y = GetElement(3, 1);
            result.z = GetElement(3, 2);
        }

        public void SetElements(Matrix4X4 CopyFrom)
        {
            SetElements(CopyFrom.GetElements());
        }

        public void SetElements(double[] pElements)
        {
            for(int i=0; i<16; i++)
            {
                matrix[i] = pElements[i];
            }
        }

        public void SetElements(double A00_00, double A00_01, double A00_02, double A00_03,
                                     double A01_00, double A01_01, double A01_02, double A01_03,
                                     double A02_00, double A02_01, double A02_02, double A02_03,
                                     double A03_00, double A03_01, double A03_02, double A03_03)
        {
            int Offset = 0;
            matrix[Offset++] = A00_00; matrix[Offset++] = A00_01; matrix[Offset++] = A00_02; matrix[Offset++] = A00_03;
            matrix[Offset++] = A01_00; matrix[Offset++] = A01_01; matrix[Offset++] = A01_02; matrix[Offset++] = A01_03;
            matrix[Offset++] = A02_00; matrix[Offset++] = A02_01; matrix[Offset++] = A02_02; matrix[Offset++] = A02_03;
            matrix[Offset++] = A03_00; matrix[Offset++] = A03_01; matrix[Offset++] = A03_02; matrix[Offset++] = A03_03;
        }

        public void SetElements(float A00_00, float A00_01, float A00_02, float A00_03,
                                     float A01_00, float A01_01, float A01_02, float A01_03,
                                     float A02_00, float A02_01, float A02_02, float A02_03,
                                     float A03_00, float A03_01, float A03_02, float A03_03)
        {
            SetElements((double)A00_00, (double)A00_01, (double)A00_02, (double)A00_03,
                                     (double)A01_00, (double)A01_01, (double)A01_02, (double)A01_03,
                                     (double)A02_00, (double)A02_01, (double)A02_02, (double)A02_03,
                                     (double)A03_00, (double)A03_01, (double)A03_02, (double)A03_03);
        }

        public double[] GetElements()
        {
	        return matrix;
        }

        public override string ToString()
        {
            string finalString = "Matrix4x4 (";
            for(int i=0; i<16; i++)
            {
                finalString += this[i].ToString() + ", ";
            }
            finalString += ")";
            return finalString;
        }
    }
};