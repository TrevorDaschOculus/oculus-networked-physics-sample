/**
 * Copyright (c) 2017-present, Facebook, Inc.
 * All rights reserved.
 *
 * This source code is licensed under the BSD-style license found in the
 * LICENSE file in the Scripts directory of this source tree. An additional grant 
 * of patent rights can be found in the PATENTS file in the same directory.
 */

using System;
using UnityEngine;
using UnityEngine.Assertions;
using System.Collections.Generic;
using Oculus.Avatar2;
using UnityEngine.Events;

public class RemoteAvatar : MonoBehaviour
{
    const float LineWidth = 0.25f;

    int clientIndex;

    public void SetClientIndex( int clientIndex )
    {
        this.clientIndex = clientIndex;
    }

    public class HandData
    {
        public Transform transform;
        public GameObject pointLine;
        public GameObject gripObject;
    };

    HandData leftHand = new HandData();
    HandData rightHand = new HandData();
    SampleAvatarEntity oculusAvatar;
    Context context;
    private uint avatarReadLength = 0;
    private byte[] avatarReadBuffer = Array.Empty<byte>(); // default to empty

    public HandData GetLeftHand() { return leftHand; }
    public HandData GetRightHand() { return rightHand; }

    public void SetContext( Context context )
    {
        this.context = context;
    }

	void Start()
    {
        oculusAvatar = (SampleAvatarEntity) GetComponent( typeof( SampleAvatarEntity ) );

        oculusAvatar.OnSkeletonLoadedEvent.AddListener(SkeletonLoadedEvent);
	}
    
    private void SkeletonLoadedEvent(OvrAvatarEntity entity)
    {
        leftHand.transform = oculusAvatar.GetSkeletonTransform(CAPI.ovrAvatar2JointType.LeftHandIndexProximal);
        rightHand.transform = oculusAvatar.GetSkeletonTransform(CAPI.ovrAvatar2JointType.RightHandIndexProximal);
    }

    void CreatePointingLine( ref HandData hand )
    {
        if ( !hand.pointLine )
        {
            hand.pointLine = (GameObject) Instantiate( context.remoteLinePrefabs[clientIndex], Vector3.zero, Quaternion.identity );

            Assert.IsNotNull( hand.pointLine );

            UpdatePointingLine( ref hand );
        }
    }

    void UpdatePointingLine( ref HandData hand )
    {
        if ( hand.pointLine )
        {
            var lineRenderer = hand.pointLine.GetComponent<LineRenderer>();

            Vector3 start = hand.transform.position;
            Vector3 finish = hand.gripObject.transform.position;

            if ( lineRenderer )
            {
                if ( ( finish - start ).magnitude >= 1 )
                {
                    lineRenderer.positionCount = 2;
                    lineRenderer.SetPosition( 0, start );
                    lineRenderer.SetPosition( 1, finish );
                    lineRenderer.startWidth = LineWidth;
                    lineRenderer.endWidth = LineWidth;
                }
                else
                {
                    lineRenderer.positionCount = 0;
                }
            }
        }
    }

    void DestroyPointingLine( ref HandData hand )
    {
        if ( hand.pointLine )
        {
            DestroyObject( hand.pointLine );

            hand.pointLine = null;
        }
    }

    public void CubeAttached( ref HandData hand )
    {
        CreatePointingLine( ref hand );
    }

    public void CubeDetached( ref HandData hand )
    {
        if ( !hand.gripObject )
            return;

        DestroyPointingLine( ref hand );

        var rigidBody = hand.gripObject.GetComponent<Rigidbody>();

        rigidBody.isKinematic = false;
        rigidBody.detectCollisions = true;

        hand.gripObject.transform.SetParent( null );

        hand.gripObject = null;
    }

    public void Update()
    {
        UpdateHand( ref leftHand );
        UpdateHand( ref rightHand );

        UpdatePointingLine( ref leftHand );
        UpdatePointingLine( ref rightHand );
    }

    public void UpdateHand( ref HandData hand )
    {
        if ( hand.gripObject )
        {
            // while an object is held, set its last interaction frame to the current sim frame. this is used to boost priority for the object when it is thrown.
            NetworkInfo networkInfo = hand.gripObject.GetComponent<NetworkInfo>();
            networkInfo.SetLastPlayerInteractionFrame( (long) context.GetSimulationFrame() );
        }
    }

    public bool GetAvatarState( out AvatarState avatarState )
    {
        AvatarState.Initialize( out avatarState, clientIndex, avatarReadBuffer, avatarReadLength, leftHand.gripObject, rightHand.gripObject );
        return true;
    }

    public void ApplyLeftHandUpdate( ref AvatarState avatarState )
    {
        AvatarState.ApplyLeftHandUpdate( ref avatarState, clientIndex, context, this );
    }

    public void ApplyRightHandUpdate( ref AvatarState avatarState )
    {
        AvatarState.ApplyRightHandUpdate( ref avatarState, clientIndex, context, this );
    }

    public GameObject GetHead()
    {
        return oculusAvatar.GetSkeletonTransform(CAPI.ovrAvatar2JointType.Head).gameObject;
    }

    public void ApplyAvatarPose(ref AvatarState avatarStreamState)
    {
        if (avatarStreamState.stream_length == 0)
            return;
        
        // copy to our read buffer to echo back if requested
        if (avatarStreamState.stream_length > avatarReadBuffer.Length)
            Array.Resize(ref avatarReadBuffer, (int)(avatarStreamState.stream_length * 2));

        Buffer.BlockCopy(avatarStreamState.stream_data, 0, avatarReadBuffer, 0, (int)avatarStreamState.stream_length);
        avatarReadLength = avatarStreamState.stream_length;
        
        oculusAvatar.ApplyStreamData(avatarStreamState.stream_data, avatarStreamState.stream_length);
    }

    public void LoadAvatar( ulong userId, int anonymousId )
    {
        if (oculusAvatar == null)
            oculusAvatar = GetComponent<SampleAvatarEntity>();
        
        if (!oculusAvatar.IsCreated)
        {
            oculusAvatar.CreateEntity();
        }
        if (userId == 0)
        {
            oculusAvatar.LoadPreset( anonymousId % 32 );
        }
        else
        {
            oculusAvatar.LoadRemoteUserCdnAvatar( userId );
        }
    }

    public void UnloadAvatar()
    {
        if (oculusAvatar != null)
            oculusAvatar.Teardown();
    }
}

