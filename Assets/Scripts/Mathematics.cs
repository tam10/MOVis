using System.Linq;
using UnityEngine;
using System.Collections.Generic;

public static class Mathematics {
    public static Matrix4x4 GetInertiaTensor(Vector3[] positions, float[] masses) {
        int numSpheres = positions.Length;
        Vector3 massWeightedAveragePosition = Enumerable.Aggregate(Enumerable.Range(0, numSpheres).Select(i => positions[i] * masses[i]), (s,v) => s+v);
        Vector3 centreOfMass = massWeightedAveragePosition / masses.Sum();

        Matrix4x4 inertiaTensor = new Matrix4x4();
        for (int i=0; i<numSpheres; i++) {
            float m = masses[i];
            Vector3 p = positions[i] - centreOfMass;
            inertiaTensor.m00 += m * (p.y * p.y + p.z * p.z);
            inertiaTensor.m11 += m * (p.x * p.x + p.z * p.z);
            inertiaTensor.m22 += m * (p.x * p.x + p.y * p.y);

            inertiaTensor.m01 -= m * (p.x * p.y);
            inertiaTensor.m02 -= m * (p.x * p.z);
            inertiaTensor.m12 -= m * (p.y * p.z);

        }
        inertiaTensor.m10 = inertiaTensor.m01;
        inertiaTensor.m20 = inertiaTensor.m02;
        inertiaTensor.m21 = inertiaTensor.m12;

        inertiaTensor.m33 = 1;

        return inertiaTensor;
    }

    public static float Det(Matrix4x4 A) {
        return (
            + A.m00 * A.m11 * A.m22 
            + A.m01 * A.m12 * A.m20
            + A.m02 * A.m10 * A.m21
            - A.m02 * A.m11 * A.m20 
            - A.m01 * A.m10 * A.m22
            - A.m00 * A.m12 * A.m21
        );
    }

    public static float Det(Vector3 a0, Vector3 a1, Vector3 a2) {
        return (
            + a0.x * a1.y * a2.z 
            + a0.y * a1.z * a2.x
            + a0.z * a1.x * a2.y
            - a0.z * a1.y * a2.x 
            - a0.y * a1.x * a2.z
            - a0.x * a1.z * a2.y
        );
    }

    public static float Sq(float v) => v*v;

    public static Vector3 GetEigenValues(Matrix4x4 A) {
        float p1 = Sq(A.m01) + Sq(A.m02) + Sq(A.m12);
        
        if (p1 == 0) {
            return new Vector3(A.m00, A.m11, A.m22);
        }
        float q = (A.m00 + A.m11 + A.m22) / 3;
        float p2 = Sq(A.m00 - q) + Sq(A.m11 - q) + Sq(A.m22 - q) + 2 * p1;
        float p = Mathf.Sqrt(p2 / 6);
        Vector3 b0 = new Vector3((A.m00 - q) / p,  A.m01      / p,  A.m02      / p);
        Vector3 b1 = new Vector3( A.m10      / p, (A.m11 - q) / p,  A.m12      / p);
        Vector3 b2 = new Vector3( A.m20      / p,  A.m21      / p, (A.m22 - q) / p);

        float r = Det(b0,b1,b2) / 2;

        float phi;
        if (r <= -1) {
            phi = Mathf.PI / 3;
        } else if (r >= 1) {
            phi = 0;
        } else {
            phi = Mathf.Acos(r) / 3;
        }

        float eig0 = q + 2 * p * Mathf.Cos(phi);
        float eig1 = q + 2 * p * Mathf.Cos(phi + (2*Mathf.PI/3));
        float eig2 = 3 * q - eig0 - eig1;

        List<float> eigs = new List<float> {eig0,eig1,eig2};
        eigs.Sort();

        return new Vector3(
            eigs[2],
            eigs[1],
            eigs[0]
        );
        
    }

    public static Matrix4x4 GetEigenVectors(Matrix4x4 A, Vector3 e) {
        Matrix4x4 res = new Matrix4x4();
        res.SetRow(0, GetEigenVector(A, e.x));
        res.SetRow(1, GetEigenVector(A, e.y));
        res.SetRow(2, GetEigenVector(A, e.z));
        return res;
    }

    public static Vector3 GetEigenVector(Matrix4x4 A, float e) {
        Matrix4x4 L = new Matrix4x4(A.GetColumn(0), A.GetColumn(1), A.GetColumn(2), A.GetColumn(3));
        
        // static Vector3 EigenVector(Vector3 rX, Vector3 rY, Vector3 rZ, float lambda)
        // {
        //     // Move RHS to LHS
        //     rX.X -= lambda;
        //     rY.Y -= lambda;
        //     // Transform to upper triangle
        //     rY -= rX * (rY.X / rX.X);
        //     // Backsubstitute
        //     var res = new Vector3(1f);
        //     res.Y = -rY.Z / rY.Y;
        //     res.X = -(rX.Y * res.Y + rX.Z * res.Z) / rX.X;
        //     return res;
        // }

        // Move RHS to LHS
        L.m00 -= e;
        L.m11 -= e;
        // Transform to upper triangle
        L.SetRow(1, L.GetRow(1) - L.GetRow(0) * (L.m10 / L.m00));
        // Backsubstitute
        Vector3 res = Vector3.one;
        res.y = -L.m12 / L.m11;
        res.x = -(L.m01 * res.y + L.m02 * res.z) / L.m00;
        return res.normalized;

    }
}