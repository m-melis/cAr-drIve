﻿using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using UnityEngine;

public class CarAgent : Agent
{
    public float speed = 10f;
    public float torque = 3f;

    public int score = 0;
    public bool resetOnCollision = true;

    private Transform _track;

    public override void Initialize()
    {
        GetTrackIncrement();
    }

    private void MoveCar(float horizontal, float vertical, float dt)
    {
        float ray1 = ObserveRay(1.5f, .5f, 25f, 5f, true);
        float ray2 = ObserveRay(1.5f, 0f, 0f, 5f, true);
        float ray3 = ObserveRay(1.5f, -.5f, -25f, 5f, true);
        float ray4 = ObserveRay(0f, .5f, 40f, 2f, true);

        float moveSpeed = speed;
        
        // Clear ahead bonus
        if (ray1 <= 0 && ray2 <= 0 && ray3 <= 0)
        {
            moveSpeed *= 1.1f;
        }
        
        // Overtake bonus
        if (ray4 > 0)
        {
            moveSpeed *= 1.3f;
        }
        
        // Blocked ahead malus
        moveSpeed = reduceSpeedOnCollision(moveSpeed, ray1);
        moveSpeed = reduceSpeedOnCollision(moveSpeed, ray2);
        moveSpeed = reduceSpeedOnCollision(moveSpeed, ray3);
        
        float distance = moveSpeed * vertical;

        transform.Translate(distance * dt * Vector3.forward);

        float rotation = horizontal * torque * 90f;
        transform.Rotate(0f, rotation * dt, 0f);
    }

    private float reduceSpeedOnCollision(float moveSpeed, float collisionDistance)
    {
        if (collisionDistance > 0 && collisionDistance < .5f)
        {
            moveSpeed *= collisionDistance;
            Debug.Log(this.name + ": " + moveSpeed);
        }

        return moveSpeed;
    }

    public override void OnActionReceived(float[] vectorAction)
    {
        float horizontal = vectorAction[0];
        float vertical = vectorAction[1];

        var lastPos = transform.position;
        MoveCar(horizontal, vertical, Time.fixedDeltaTime);

        int reward = GetTrackIncrement();

        var moveVec = transform.position - lastPos;
        float angle = Vector3.Angle(moveVec, _track.forward);
        float bonus = (1f - angle / 90f) * Mathf.Clamp01(vertical) * Time.fixedDeltaTime;
        AddReward(bonus + reward);

        score += reward;
    }

    public override void Heuristic(float[] actionsOut)
    {
        actionsOut[0] = Input.GetAxis("Horizontal");
        actionsOut[1] = Input.GetAxis("Vertical");
    }

    public override void CollectObservations(VectorSensor vectorSensor)
    {
        float angle = Vector3.SignedAngle(_track.forward, transform.forward, Vector3.up);

        vectorSensor.AddObservation(angle / 180f);
        vectorSensor.AddObservation(ObserveRay(1.5f, .5f, 25f));
        vectorSensor.AddObservation(ObserveRay(1.5f, 0f, 0f));
        vectorSensor.AddObservation(ObserveRay(1.5f, -.5f, -25f));
        vectorSensor.AddObservation(ObserveRay(-1.5f, 0, 180f));
    }

    private float ObserveRay(
        float z, float x, float angle, float rayDist = 5f, bool carOnly = false)
    {
        var tf = transform;

        // Get the start position of the ray
        var raySource = tf.position + Vector3.up / 2f;
        var position = raySource + tf.forward * z + tf.right * x;

        // Get the angle of the ray
        var eulerAngle = Quaternion.Euler(0, angle, 0f);
        var dir = eulerAngle * tf.forward;

        // See if there is a hit in the given direction
        Physics.Raycast(position, dir, out var hit, rayDist);

        if (hit.collider != null)
        {
            if (hit.collider.gameObject.CompareTag("car"))
            {
                Debug.Log(this.name + ": collision " + hit.collider.name);
                Debug.Log(this.name + ": collision in " + hit.distance / rayDist);
            }
            else if (carOnly)
            {
                return -1f;
            }
        }

        return hit.distance >= 0 ? hit.distance / rayDist : -1f;
    }

    private int GetTrackIncrement()
    {
        int reward = 0;
        var carCenter = transform.position + Vector3.up;

        // Find what tile I'm on
        if (Physics.Raycast(carCenter, Vector3.down, out var hit, 2f))
        {
            var newHit = hit.transform;
            // Check if the tile has changed
            if (_track != null && newHit != _track)
            {
                float angle = Vector3.Angle(_track.forward, newHit.position - _track.position);
                reward = (angle < 90f) ? 1 : -1;
            }

            _track = newHit;
        }

        return reward;
    }

    public override void OnEpisodeBegin()
    {
        if (resetOnCollision)
        {
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }
    }

    private void OnCollisionEnter(Collision other)
    {
        if (other.gameObject.CompareTag("wall") ||
            other.gameObject.CompareTag("car"))
        {
            SetReward(-1f);
            EndEpisode();
        }
    }
}