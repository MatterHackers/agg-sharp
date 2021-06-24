public class SMat3
    {
        public double m00, m01, m02, m11, m12, m22;

        public SMat3()
        { 
            clear();
        }

        public SMat3(double m00, double m01, double m02, double m11, double m12, double m22)
        { 
            this.setSymmetric(m00, m01, m02, m11, m12, m22);
        }

        public void clear() 
        {
            this.setSymmetric(0, 0, 0, 0, 0, 0); 
        }

        public void setSymmetric(double a00, double a01, double a02, double a11, double a12, double a22)
        {
            this.m00 = a00;
            this.m01 = a01;
            this.m02 = a02;
            this.m11 = a11;
            this.m12 = a12;
            this.m22 = a22;
        }

        public void SetSymmetric(SMat3 rhs)
        {
            this.setSymmetric(rhs.m00, rhs.m01, rhs.m02, rhs.m11, rhs.m12, rhs.m22);
        }

        private SMat3(SMat3 rhs) 
        { 
            this.SetSymmetric(rhs);
        }
    }