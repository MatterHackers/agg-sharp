using System;

public class Mat3
{
    public double m00, m01, m02, m10, m11, m12, m20, m21, m22;

    public void clear() { }

	public void SetSymmetric(SMat3 a)
	{
		SetSymmetric(a.m00, a.m01, a.m02, a.m11, a.m12, a.m22);
	}

	public void SetSymmetric(double a00, double a01, double a02, double a11, double a12, double a22)
	{
		this.m00 = a00;
		this.m01 = a01;
		this.m02 = a02;
		this.m10 = a01;
		this.m11 = a11;
		this.m12 = a12;
		this.m20 = a02;
		this.m21 = a12;
		this.m22 = a22;
	}

	public void Set(double m00, double m01, double m02, double m10, double m11, double m12, double m20, double m21, double m22)
	{
		this.m00 = m00;
		this.m01 = m01;
		this.m02 = m02;
		this.m10 = m10;
		this.m11 = m11;
		this.m12 = m12;
		this.m20 = m20;
		this.m21 = m21;
		this.m22 = m22;
	}
}
