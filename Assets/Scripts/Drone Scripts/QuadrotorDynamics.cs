﻿namespace ISAACS
{
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using UnityEngine;

	public static class QuadrotorDynamics
	{

		// TODO: documentation
		// inputs:
		// -> frame = velocity_B (velocities u, v, w in the body frame)
		// -> rotation = angularPosition_I (phi, theta, psi)
		// returns the velocity_I the velocity in the inertial frame
		public static Vector3 Rotation(Vector3 frame, Vector3 rotation, bool degrees=true)
		{
			float ux = rotation.x;
			float uy = rotation.y;
			float uz = rotation.z;

			// If theta was given in degrees, convert it to radians.	
			if (degrees)
			{
				ux *= (float)Math.PI / 180.0f;
				uy *= (float)Math.PI / 180.0f;
				uz *= (float)Math.PI / 180.0f;
			}

			float sux = (float)Math.Sin(ux);
			float cux = (float)Math.Cos(ux);
			float suy = (float)Math.Sin(uy);
			float cuy = (float)Math.Cos(uy);
			float suz = (float)Math.Sin(uz);
			float cuz = (float)Math.Cos(uz);

			Vector3 R1 = new Vector3(suz * sux * suy + cuz * cuy,      cuz * sux * suy - suz * cuy,      cux * suy);
			Vector3 R2 = new Vector3(suz * cux,                        cuz * cux,                        -sux);
			Vector3 R3 = new Vector3(suz * sux * cuy - cuz * suy,      cuz * sux * cuy + suz * suy,      cux * cuy);

			Vector3 rotatedFrame = frame;	
			rotatedFrame.x = Vector3.Dot(R1, frame);
			rotatedFrame.y = Vector3.Dot(R2, frame);
			rotatedFrame.z = Vector3.Dot(R3, frame);

			return rotatedFrame;
		}

		// TODO: documentation
		// phi dot, theta dot, psi dot
		// inputs:
		// -> direction = angular_velocity_B (angular_velocities p, r, q in the body frame)
		// -> rotation = angularPosition_I (phi, theta, psi)
		// return w = w_I = angularVelocity_I (in the inertial frame)	
		public static Vector3 InverseJacobian(Vector3 direction, Vector3 rotation, bool degrees=true)
		{
			float p = direction.x;
			float r = direction.y;
			float q = direction.z;

			float ux = rotation.x;
			float uz = rotation.z;

			if (degrees)
			{
				ux *= (float)Math.PI / 180.0f;
				uz *= (float)Math.PI / 180.0f;
			}

			float cos_ux = (float)Math.Cos(ux);
			float tan_ux = (float)Math.Tan(ux);
			float sin_uz = (float)Math.Sin(uz);
			float cos_uz = (float)Math.Cos(uz);

			float dx = r * cos_uz - p * sin_uz;
			float dy = p * (cos_uz / cos_ux) + r * (sin_uz / cos_uz);
			float dz = q + p * cos_uz * tan_ux + r * sin_uz * tan_ux;

			return new Vector3(dx, dy, dz);
		}
		
		public static Vector4 TargetRotorSpeeds(float target_speed, Vector3 destination, Vector3 position,
										    	Vector3 velocity, Vector3 acceleration, float mass, float g,
												Vector3 inertia, Vector3 angular_position, Vector3 angular_velocity,
												float drag_factor, float thrust_factor, float rod_length, float yaw_factor,
												bool degrees=true)
		{
				// if (!degrees)
				// {
				// 	angular_position.x *= 180.0f / (float)Math.PI;
				// 	angular_position.y *= 180.0f / (float)Math.PI;
				// 	angular_position.z *= 180.0f / (float)Math.PI;
					
				// 	angular_velocity.x *= 180.0f / (float)Math.PI;
				// 	angular_velocity.y *= 180.0f / (float)Math.PI;
				// 	angular_velocity.z *= 180.0f / (float)Math.PI;
				// }	


                Vector3 targetVelocity = (destination - position).normalized;
                Debug.Log("targetVelocity BEFORE SPEED x: " + targetVelocity.x + " y: " + targetVelocity.y + " z: " + targetVelocity.z);
				Debug.Log("targetSpeed: " + target_speed);
				targetVelocity *= target_speed;
                Debug.Log("targetVelocity x: " + targetVelocity.x + " y: " + targetVelocity.y + " z: " + targetVelocity.z);
                Vector3 targetAcceleration = targetVelocity - velocity;
                Debug.Log("targetAcceleration x: " + targetAcceleration.x + " y: " + targetAcceleration.y + " z: " + targetAcceleration.z);

				// Cosines and sines cancel out, to give thrust_acceleration
				float thrust_acceleration = Mathf.Sqrt(targetAcceleration.x * targetAcceleration.x
													  + (targetAcceleration.y - g) * (targetAcceleration.y - g)
													  + targetAcceleration.z * targetAcceleration.z);

				Debug.Log("thrust_acceleration: " + thrust_acceleration);
				float thrust = mass * thrust_acceleration;
				Debug.Log("mass: " + mass);	
				Debug.Log("thrust: " + thrust);	
				Vector4 targetTorques;
                targetTorques.w = thrust;

				// targetAngularPosition.x /= thrust_acceleration;
				// targetAngularPosition.y -= g;
				// targetAngularPosition.y /= thrust_acceleration;
				// targetAngularPosition.z /= thrust_acceleration;
				// targetAngularPosition = targetAngularPosition.normalized;

				// Vector3 targetAngularPosition = targetAcceleration.normalized;

				Vector3 targetAngularAcceleration = targetAcceleration.normalized;

				float Ixx = inertia.x;
				float Iyy = inertia.y;
				float Izz = inertia.z;
                Debug.Log("++++++++INERTIA++++++++++++ Ixx: " + inertia.x + " Iyy: " + inertia.y + " Izz: " + inertia.z);
				// float dux = angular_velocity.x;
				// float duy = angular_velocity.y;
				// float duz = angular_velocity.z;
				targetTorques.x = targetAngularAcceleration.x * Ixx;
				targetTorques.y = targetAngularAcceleration.y * Iyy;
				targetTorques.z = targetAngularAcceleration.z * Izz;
				// targetTorques.x = targetAngularAcceleration.x * Ixx	- (Iyy - Izz) * dux;
				// targetTorques.y = targetAngularAcceleration.y * Iyy	- (Izz - Ixx) * duy;
				// targetTorques.z = targetAngularAcceleration.z * Izz	- (Ixx - Iyy) * duz;
				// targetTorques.x = targetAngularAcceleration.x * Ixx	- (Iyy - Izz) * duy * duz;
				// targetTorques.y = targetAngularAcceleration.y * Iyy	- (Izz - Ixx) * duz * dux;
				// targetTorques.z = targetAngularAcceleration.z * Izz	- (Ixx - Iyy) * dux * duy;
                Debug.Log("++++++++TORQUES++++++++++++ w: " + targetTorques.w + " x: " + targetTorques.x + " y: " + targetTorques.y + " z: " + targetTorques.z);

				// Find the constants needed to compute the target rotor speeds
				float W = targetTorques.w / (drag_factor * thrust_factor);
				float X = targetTorques.x / (rod_length * thrust_factor);
				float Y = targetTorques.y / yaw_factor;
				float Z = targetTorques.z / (rod_length * thrust_factor);
                Debug.Log("CONSTANTS W: " + W + " x: " + X + " y: " + Y + " z: " + Z);

				Vector4 targetRotorSpeeds;
				float f1 = (W - Y - 2.0f * X) / 4.0f;
				float f3 = X + f1;
				float f2 = (Y - Z + f1 + f3) / 2.0f;
				float f4 = Z + f2;
				// if (f1 < 0)
				// {
				// 	f2 += f1 / 2.0f;
				// 	f4 += f1 / 2.0f;
				// 	f1 = 0.0f;
				// }
				// if (f2 < 0)
				// {
				// 	f1 += f2 / 2.0f;
				// 	f3 += f2 / 2.0f;
				// 	f2 = 0.0f;
				// }
				// if (f3 < 0)
				// {
				// 	f2 += f3 / 2.0f;
				// 	f4 += f3 / 2.0f;
				// 	f3 = 0.0f;
				// }
				// if (f4 < 0)
				// {
				// 	f1 += f4 / 2.0f;
				// 	f3 += f4 / 2.0f;
				// 	f4 = 0.0f;
				// }
				// float estimatedThrust = drag_factor * thrust_factor * (f1 + f2 + f3 + f4);
				// if (estimatedThrust > thrust)
				// {
				// 	float scaling_factor = thrust / estimatedThrust;
				// 	f1 *= scaling_factor;	
				// 	f2 *= scaling_factor;	
				// 	f3 *= scaling_factor;	
				// 	f4 *= scaling_factor;	
				// }
				// targetRotorSpeeds.w = (float)Math.Sqrt(f1);
				// targetRotorSpeeds.x = (float)Math.Sqrt(f2);
				// targetRotorSpeeds.y = (float)Math.Sqrt(f3);
				// targetRotorSpeeds.z = (float)Math.Sqrt(f4);
				targetRotorSpeeds.w = f1;
				targetRotorSpeeds.x = f2;
				targetRotorSpeeds.y = f3;
				targetRotorSpeeds.z = f4;

				return targetRotorSpeeds;
		}	

		/*
				f1
				O-->	
			  /   \
		  f4 O-->  O--> f2     
			  \   /
				O-->
				f3	

		x-axis: f2 to f4
		y-axis: out of the page
		z-axis: f1 to f3
		*/	
		/// <summary>
        /// Given the individual thrust of each rotor ("rotor forces"), this method returns the quadrotor's total thrust and torques.
		/// To simplify things, only the sign and magnitude of each rotor force is required, and only the sign and magnitude of 
		/// the total thrust and torques is returned.
        /// </summary>
        /// <param name="rotorForces">The signed magnitude of the force exterted by each rotor, in the order shown in the diagram above.</param>
        /// <param name="dragFactor">A damping factor for the thrust, representing resistance by the drag.</param>
        /// <param name="thrustFactor">The contribution of each rotor force to the total thrust, as well as its x-torque ("roll force") and z-torque ("pitch force").</param>
        /// <param name="rodLength">The distance between two opposing rotors, such as O(f1) and O(f3). It is assumed that O(f1)->O(f3) and O(f2)->O(f4) are equal.</param>
        /// <param name="yawFactor">The contribution of each rotor force to the y-torque ("yaw force").</param>
        /// <returns>The signed mangitudes of the quadrotor's total thrust and torques, for the given rotor forces.</returns>
		public static Vector4 SpinRotors(Vector4 rotorForces, float dragFactor, float thrustFactor, float rodLength, float yawFactor)
		{	
			float f1 = rotorForces.w;
			float f2 = rotorForces.x;
			float f3 = rotorForces.y;
			float f4 = rotorForces.z;

			Vector4 torques;
			torques.w = dragFactor * thrustFactor * (f1 + f2 + f3 + f4); // thrust from all four rotors, accelerating the quadrotor in the direction of its y-axis
			torques.x = rodLength  * thrustFactor * (f3 - f1);           // torque rotating the quadrotor around its x-axis ("roll force")
			torques.y = yawFactor  * (f2 + f4 - f1 - f3);                // torque rotating the quadrotor around its y-axis ("yaw force")
			torques.z = rodLength  * thrustFactor * (f4 - f2);           // torque rotating the quadrotor around its z-axis ("pitch force")

			return torques;
		}

		// TODO: documentation
		// Returns the acceleration in the inertial frame
		// **x, **y, **z
		public static Vector3 Acceleration(float thrust, float mass, float g, Vector3 angular_position, bool degrees=true)
		{
			float ux = angular_position.x;
			float uy = angular_position.y;
			float uz = angular_position.z;

			// if (degrees)
			// {
			// 	ux *= (float)Math.PI / 180.0f;
			// 	uy *= (float)Math.PI / 180.0f;
			// 	uz *= (float)Math.PI / 180.0f;
			// }

			// float sin_ux = (float)Math.Sin(ux);
			// float cos_ux = (float)Math.Cos(ux);
			// float sin_uy = (float)Math.Sin(uy);
			// float cos_uy = (float)Math.Cos(uy);
			// float sin_uz = (float)Math.Sin(uz);
			// float cos_uz = (float)Math.Cos(uz);

			float thrust_acceleration = thrust / mass;

			Vector3 acceleration;
			// acceleration.x =     thrust_acceleration * (cos_uz * sin_uy * sin_ux - cos_uy * sin_uz);
			// acceleration.y = g + thrust_acceleration * (cos_uz * cos_ux);
			// acceleration.z =     thrust_acceleration * (sin_uz * sin_uy + cos_uz * cos_uy * sin_ux);
			acceleration.x =     thrust_acceleration * ux;
			acceleration.y = g + thrust_acceleration * uy;
			acceleration.z =     thrust_acceleration * uz;

			return acceleration;
		}    

		// TODO: documentation
		// Returns the angular acceleration in the inertial frame
		// **phi, **theta, **psi
		public static Vector3 AngularAcceleration(Vector4 torques, Vector3 inertia, Vector3 angular_velocity, bool degrees=true)
		{
			float Ixx = inertia.x;
			float Iyy = inertia.y;
			float Izz = inertia.z;

			// float dux = angular_velocity.x;
			// float duy = angular_velocity.y;
			// float duz = angular_velocity.z;

			// if (!degrees)
			// {
			// 	dux *= 180.0f / (float)Math.PI;
			// 	duy *= 180.0f / (float)Math.PI;
			// 	duz *= 180.0f / (float)Math.PI;
			// }

			Vector3 angularAcceleration;
			// angularAcceleration.x = ((Iyy - Izz) * dux + torques.x) / Ixx;
			// angularAcceleration.y = ((Izz - Ixx) * duy + torques.y) / Iyy;
			// angularAcceleration.z = ((Ixx - Iyy) * duz + torques.z) / Izz;
			angularAcceleration.x = torques.x / Ixx;
			angularAcceleration.y = torques.y / Iyy;
			angularAcceleration.z = torques.z / Izz;
			// angularAcceleration.x = ((Iyy - Izz) * duy * duz + torques.x) / Ixx;
			// angularAcceleration.y = ((Izz - Ixx) * duz * dux + torques.y) / Iyy;
			// angularAcceleration.z = ((Ixx - Iyy) * dux * duy + torques.z) / Izz;

			return angularAcceleration;	
		}    

		// TODO: documentation
		public static Vector3 AccelerationBody(float thrust, float mass, float g, Vector3 wind_disturbance, Vector3 velocity_body, Vector3 angular_velocity_body, Vector3 angular_position, bool degrees=true)
		{
			float dx_b = velocity_body.x;
			float dy_b = velocity_body.y;
			float dz_b = velocity_body.z;

			float dux_b = angular_velocity_body.x;
			float duy_b = angular_velocity_body.y;
			float duz_b = angular_velocity_body.z;

			if (!degrees)
			{
				dux_b *= 180.0f / (float)Math.PI;
				duy_b *= 180.0f / (float)Math.PI;
				duz_b *= 180.0f / (float)Math.PI;
			}

			float ux = angular_position.x;
			float uz = angular_position.z;

			if (degrees)
			{
				ux *= (float)Math.PI / 180.0f;
				uz *= (float)Math.PI / 180.0f;
			}

			float sin_ux = (float)Math.Sin(ux);
			float cos_ux = (float)Math.Cos(ux);
			float sin_uz = (float)Math.Sin(uz);
			float cos_uz = (float)Math.Cos(uz);


			Vector3 acceleration_body;
			acceleration_body.x = duz_b * dy_b - duy_b * dz_b + g * sin_uz * cos_ux + wind_disturbance.x / mass;
			acceleration_body.y = dux_b * dz_b - duz_b * dx_b + g * cos_ux * cos_uz + (wind_disturbance.y - thrust) / mass;
			acceleration_body.z = duy_b * dx_b - dux_b * dy_b - g * sin_ux          + wind_disturbance.z / mass;

			return acceleration_body;
		}

		// TODO: documentation
		public static Vector3 AngularAccelerationBody(Vector4 torques, Vector3 inertia, Vector3 angular_wind_disturbance, Vector3 angular_velocity_body, bool degrees=true)
		{
			float torque_x = torques.x;
			float torque_y = torques.y;
			float torque_z = torques.z;

			float Ixx = inertia.x;
			float Iyy = inertia.y;
			float Izz = inertia.z;

			float dux_b = angular_velocity_body.x;
			float duy_b = angular_velocity_body.y;
			float duz_b = angular_velocity_body.z;

			float wx = angular_wind_disturbance.x;
			float wy = angular_wind_disturbance.y;
			float wz = angular_wind_disturbance.z;

			if (!degrees)
			{
				dux_b *= 180.0f / (float)Math.PI;
				duy_b *= 180.0f / (float)Math.PI;
				duz_b *= 180.0f / (float)Math.PI;

				wx *= 180.0f / (float)Math.PI;
				wy *= 180.0f / (float)Math.PI;
				wz *= 180.0f / (float)Math.PI;
			}

			Vector3 angular_acceleration_body;
			angular_acceleration_body.x = ((Iyy - Izz) * duy_b * duz_b + torque_x + wx) / Ixx;
			angular_acceleration_body.y = ((Izz - Ixx) * dux_b * duz_b + torque_y + wy) / Iyy;
			angular_acceleration_body.z = ((Ixx - Iyy) * dux_b * duy_b + torque_z + wz) / Izz;

			return angular_acceleration_body;
		}

	}
}