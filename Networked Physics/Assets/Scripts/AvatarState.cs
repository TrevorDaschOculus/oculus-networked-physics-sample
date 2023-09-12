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

public struct AvatarStateQuantized
{
    public int client_index;

    public bool left_hand_holding_cube;
    public int left_hand_cube_id;
    public ushort left_hand_authority_sequence;
    public ushort left_hand_ownership_sequence;
    public int left_hand_cube_local_position_x;
    public int left_hand_cube_local_position_y;
    public int left_hand_cube_local_position_z;
    public uint left_hand_cube_local_rotation_largest;
    public uint left_hand_cube_local_rotation_a;
    public uint left_hand_cube_local_rotation_b;
    public uint left_hand_cube_local_rotation_c;

    public bool right_hand_holding_cube;
    public int right_hand_cube_id;
    public ushort right_hand_authority_sequence;
    public ushort right_hand_ownership_sequence;
    public int right_hand_cube_local_position_x;
    public int right_hand_cube_local_position_y;
    public int right_hand_cube_local_position_z;
    public uint right_hand_cube_local_rotation_largest;
    public uint right_hand_cube_local_rotation_a;
    public uint right_hand_cube_local_rotation_b;
    public uint right_hand_cube_local_rotation_c;

    public uint stream_length;
    public byte[] stream_data;
    
    public static AvatarStateQuantized defaults;
}

public struct AvatarState
{
    public int client_index;

    public bool left_hand_holding_cube;
    public int left_hand_cube_id;
    public ushort left_hand_authority_sequence;
    public ushort left_hand_ownership_sequence;
    public Vector3 left_hand_cube_local_position;
    public Quaternion left_hand_cube_local_rotation;

    public bool right_hand_holding_cube;
    public int right_hand_cube_id;
    public ushort right_hand_authority_sequence;
    public ushort right_hand_ownership_sequence;
    public Vector3 right_hand_cube_local_position;
    public Quaternion right_hand_cube_local_rotation;

    public uint stream_length;
    public byte[] stream_data;

    public static AvatarState defaults;

    public static void Initialize( out AvatarState avatarState, int clientIndex, byte[] streamData, uint streamLength, GameObject leftHandHeldObject, GameObject rightHandHeldObject )
    {
        avatarState.client_index = clientIndex;

        if ( leftHandHeldObject )
        {
            avatarState.left_hand_holding_cube = true;

            NetworkInfo networkInfo = leftHandHeldObject.GetComponent<NetworkInfo>();

            avatarState.left_hand_cube_id = networkInfo.GetCubeId();
            avatarState.left_hand_authority_sequence = networkInfo.GetAuthoritySequence();
            avatarState.left_hand_ownership_sequence = networkInfo.GetOwnershipSequence();
            avatarState.left_hand_cube_local_position = leftHandHeldObject.transform.localPosition;
            avatarState.left_hand_cube_local_rotation = leftHandHeldObject.transform.localRotation;
        }
        else
        {
            avatarState.left_hand_holding_cube = false;
            avatarState.left_hand_cube_id = -1;
            avatarState.left_hand_authority_sequence = 0;
            avatarState.left_hand_ownership_sequence = 0;
            avatarState.left_hand_cube_local_position = Vector3.zero;
            avatarState.left_hand_cube_local_rotation = Quaternion.identity;
        }

        if ( rightHandHeldObject )
        {
            avatarState.right_hand_holding_cube = true;

            NetworkInfo networkInfo = rightHandHeldObject.GetComponent<NetworkInfo>();

            avatarState.right_hand_cube_id = networkInfo.GetCubeId();
            avatarState.right_hand_authority_sequence = networkInfo.GetAuthoritySequence();
            avatarState.right_hand_ownership_sequence = networkInfo.GetOwnershipSequence();
            avatarState.right_hand_cube_local_position = rightHandHeldObject.transform.localPosition;
            avatarState.right_hand_cube_local_rotation = rightHandHeldObject.transform.localRotation;
        }
        else
        {
            avatarState.right_hand_holding_cube = false;
            avatarState.right_hand_cube_id = -1;
            avatarState.right_hand_authority_sequence = 0;
            avatarState.right_hand_ownership_sequence = 0;
            avatarState.right_hand_cube_local_position = Vector3.zero;
            avatarState.right_hand_cube_local_rotation = Quaternion.identity;
        }

        avatarState.stream_data = streamData;
        avatarState.stream_length = streamLength;
    }

    public static void UpdateLeftHandSequenceNumbers( ref AvatarState avatarState, Context context )
    {
        if ( avatarState.left_hand_holding_cube )
        {
            var cube = context.GetCube( avatarState.left_hand_cube_id );
            var networkInfo = cube.GetComponent<NetworkInfo>();
            if ( Network.Util.SequenceGreaterThan( avatarState.left_hand_ownership_sequence, networkInfo.GetOwnershipSequence() ) )
            {
#if DEBUG_AUTHORITY
                Debug.Log( "server -> client: update left hand sequence numbers - ownership sequence " + networkInfo.GetOwnershipSequence() + "->" + state.left_hand_ownership_sequence + ", authority sequence " + networkInfo.GetOwnershipSequence() + "->" + state.left_hand_authority_sequence );
#endif // #if DEBUG_AUTHORITY
                networkInfo.SetOwnershipSequence( avatarState.left_hand_ownership_sequence );
                networkInfo.SetAuthoritySequence( avatarState.left_hand_authority_sequence );
            }
        }
    }

    public static void UpdateRightHandSequenceNumbers( ref AvatarState avatarState, Context context )
    {
        if ( avatarState.right_hand_holding_cube )
        {
            var cube = context.GetCube( avatarState.right_hand_cube_id );
            var networkInfo = cube.GetComponent<NetworkInfo>();
            if ( Network.Util.SequenceGreaterThan( avatarState.right_hand_ownership_sequence, networkInfo.GetOwnershipSequence() ) )
            {
#if DEBUG_AUTHORITY
                Debug.Log( "server -> client: update right hand sequence numbers - ownership sequence " + networkInfo.GetOwnershipSequence() + "->" + state.right_hand_ownership_sequence + ", authority sequence " + networkInfo.GetOwnershipSequence() + "->" + state.right_hand_authority_sequence );
#endif // #if DEBUG_AUTHORITY
                networkInfo.SetOwnershipSequence( avatarState.right_hand_ownership_sequence );
                networkInfo.SetAuthoritySequence( avatarState.right_hand_authority_sequence );
            }
        }
    }

    public static void ApplyLeftHandUpdate( ref AvatarState avatarState, int clientIndex, Context context, RemoteAvatar remoteAvatar )
    {
        Assert.IsTrue( clientIndex == avatarState.client_index );

        if ( avatarState.left_hand_holding_cube )
        {
            var cube = context.GetCube( avatarState.left_hand_cube_id );

            var networkInfo = cube.GetComponent<NetworkInfo>();

            if ( !networkInfo.IsHeldByRemotePlayer( remoteAvatar, remoteAvatar.GetLeftHand() ) )
            {
                networkInfo.AttachCubeToRemotePlayer( remoteAvatar, remoteAvatar.GetLeftHand(), avatarState.client_index );
            }

            networkInfo.SetAuthoritySequence( avatarState.left_hand_authority_sequence );

            networkInfo.SetOwnershipSequence( avatarState.left_hand_ownership_sequence );

            networkInfo.MoveWithSmoothingLocal( avatarState.left_hand_cube_local_position, avatarState.left_hand_cube_local_rotation );
        }
    }

    public static void ApplyRightHandUpdate( ref AvatarState avatarState, int clientIndex, Context context, RemoteAvatar remoteAvatar )
    {
        Assert.IsTrue( clientIndex == avatarState.client_index );

        if ( avatarState.right_hand_holding_cube )
        {
            GameObject cube = context.GetCube( avatarState.right_hand_cube_id );

            var networkInfo = cube.GetComponent<NetworkInfo>();

            if ( !networkInfo.IsHeldByRemotePlayer( remoteAvatar, remoteAvatar.GetRightHand() ) )
            {
                networkInfo.AttachCubeToRemotePlayer( remoteAvatar, remoteAvatar.GetRightHand(), avatarState.client_index );
            }

            networkInfo.SetAuthoritySequence( avatarState.right_hand_authority_sequence );
            networkInfo.SetOwnershipSequence( avatarState.right_hand_ownership_sequence );

            networkInfo.MoveWithSmoothingLocal( avatarState.right_hand_cube_local_position, avatarState.right_hand_cube_local_rotation );
        }
    }

    public static void Quantize( ref AvatarState avatarState, out AvatarStateQuantized quantized )
    {
        quantized.client_index = avatarState.client_index;
        
        if ( avatarState.left_hand_holding_cube )
        {
            quantized.left_hand_holding_cube = true;

            quantized.left_hand_cube_id = avatarState.left_hand_cube_id;
            quantized.left_hand_authority_sequence = avatarState.left_hand_authority_sequence;
            quantized.left_hand_ownership_sequence = avatarState.left_hand_ownership_sequence;

            quantized.left_hand_cube_local_position_x = (int) Math.Floor( avatarState.left_hand_cube_local_position.x * Constants.UnitsPerMeter + 0.5f );
            quantized.left_hand_cube_local_position_y = (int) Math.Floor( avatarState.left_hand_cube_local_position.y * Constants.UnitsPerMeter + 0.5f );
            quantized.left_hand_cube_local_position_z = (int) Math.Floor( avatarState.left_hand_cube_local_position.z * Constants.UnitsPerMeter + 0.5f );

            Snapshot.QuaternionToSmallestThree( avatarState.left_hand_cube_local_rotation,
                                                out quantized.left_hand_cube_local_rotation_largest,
                                                out quantized.left_hand_cube_local_rotation_a,
                                                out quantized.left_hand_cube_local_rotation_b,
                                                out quantized.left_hand_cube_local_rotation_c );
        }
        else
        {
            quantized.left_hand_holding_cube = false;
            quantized.left_hand_cube_id = -1;
            quantized.left_hand_authority_sequence = 0;
            quantized.left_hand_ownership_sequence = 0;
            quantized.left_hand_cube_local_position_x = 0;
            quantized.left_hand_cube_local_position_y = 0;
            quantized.left_hand_cube_local_position_z = 0;
            quantized.left_hand_cube_local_rotation_largest = 0;
            quantized.left_hand_cube_local_rotation_a = 0;
            quantized.left_hand_cube_local_rotation_b = 0;
            quantized.left_hand_cube_local_rotation_c = 0;
        }

        if ( avatarState.right_hand_holding_cube )
        {
            quantized.right_hand_holding_cube = true;

            quantized.right_hand_cube_id = avatarState.right_hand_cube_id;
            quantized.right_hand_authority_sequence = avatarState.right_hand_authority_sequence;
            quantized.right_hand_ownership_sequence = avatarState.right_hand_ownership_sequence;

            quantized.right_hand_cube_local_position_x = (int) Math.Floor( avatarState.right_hand_cube_local_position.x * Constants.UnitsPerMeter + 0.5f );
            quantized.right_hand_cube_local_position_y = (int) Math.Floor( avatarState.right_hand_cube_local_position.y * Constants.UnitsPerMeter + 0.5f );
            quantized.right_hand_cube_local_position_z = (int) Math.Floor( avatarState.right_hand_cube_local_position.z * Constants.UnitsPerMeter + 0.5f );

            Snapshot.QuaternionToSmallestThree( avatarState.right_hand_cube_local_rotation,
                                                out quantized.right_hand_cube_local_rotation_largest,
                                                out quantized.right_hand_cube_local_rotation_a,
                                                out quantized.right_hand_cube_local_rotation_b,
                                                out quantized.right_hand_cube_local_rotation_c );
        }
        else
        {
            quantized.right_hand_holding_cube = false;
            quantized.right_hand_cube_id = -1;
            quantized.right_hand_authority_sequence = 0;
            quantized.right_hand_ownership_sequence = 0;
            quantized.right_hand_cube_local_position_x = 0;
            quantized.right_hand_cube_local_position_y = 0;
            quantized.right_hand_cube_local_position_z = 0;
            quantized.right_hand_cube_local_rotation_largest = 0;
            quantized.right_hand_cube_local_rotation_a = 0;
            quantized.right_hand_cube_local_rotation_b = 0;
            quantized.right_hand_cube_local_rotation_c = 0;
        }
        // clamp everything

        if ( quantized.left_hand_holding_cube )
        {
            Snapshot.ClampLocalPosition( ref quantized.left_hand_cube_local_position_x, ref quantized.left_hand_cube_local_position_y, ref quantized.left_hand_cube_local_position_z );
        }

        if ( quantized.right_hand_holding_cube )
        {
            Snapshot.ClampLocalPosition( ref quantized.right_hand_cube_local_position_x, ref quantized.right_hand_cube_local_position_y, ref quantized.right_hand_cube_local_position_z );
        }

        quantized.stream_length = avatarState.stream_length;
        quantized.stream_data = avatarState.stream_data;
    }

    public static void Unquantize( ref AvatarStateQuantized quantized, out AvatarState avatarState )
    {
        avatarState.client_index = quantized.client_index;

        avatarState.left_hand_holding_cube = quantized.left_hand_holding_cube;
        avatarState.left_hand_cube_id = quantized.left_hand_cube_id;
        avatarState.left_hand_ownership_sequence = quantized.left_hand_ownership_sequence;
        avatarState.left_hand_authority_sequence = quantized.left_hand_authority_sequence;

        avatarState.left_hand_cube_local_position = new Vector3( quantized.left_hand_cube_local_position_x, quantized.left_hand_cube_local_position_y, quantized.left_hand_cube_local_position_z ) * 1.0f / Constants.UnitsPerMeter;
        avatarState.left_hand_cube_local_rotation = Snapshot.SmallestThreeToQuaternion( quantized.left_hand_cube_local_rotation_largest, quantized.left_hand_cube_local_rotation_a, quantized.left_hand_cube_local_rotation_b, quantized.left_hand_cube_local_rotation_c );

        avatarState.right_hand_holding_cube = quantized.right_hand_holding_cube;
        avatarState.right_hand_cube_id = quantized.right_hand_cube_id;
        avatarState.right_hand_ownership_sequence = quantized.right_hand_ownership_sequence;
        avatarState.right_hand_authority_sequence = quantized.right_hand_authority_sequence;

        avatarState.right_hand_cube_local_position = new Vector3( quantized.right_hand_cube_local_position_x, quantized.right_hand_cube_local_position_y, quantized.right_hand_cube_local_position_z ) * 1.0f / Constants.UnitsPerMeter;
        avatarState.right_hand_cube_local_rotation = Snapshot.SmallestThreeToQuaternion( quantized.right_hand_cube_local_rotation_largest, quantized.right_hand_cube_local_rotation_a, quantized.right_hand_cube_local_rotation_b, quantized.right_hand_cube_local_rotation_c );
        
        avatarState.stream_length = quantized.stream_length;
        avatarState.stream_data = quantized.stream_data;
    }

    public static void Interpolate( ref AvatarState a, ref AvatarState b, out AvatarState output, float t )
    {
        // convention: logically everything stays at the oldest sample, but positions and rotations and other continuous quantities are interpolated forward where it makes sense.

        output.client_index = a.client_index;
        
        output.left_hand_holding_cube = a.left_hand_holding_cube;
        output.left_hand_cube_id = a.left_hand_cube_id;
        output.left_hand_authority_sequence = a.left_hand_authority_sequence;
        output.left_hand_ownership_sequence = a.left_hand_ownership_sequence;

        if ( a.left_hand_holding_cube == b.left_hand_holding_cube && a.left_hand_cube_id == b.left_hand_cube_id )
        {
            output.left_hand_cube_local_position = a.left_hand_cube_local_position * ( 1 - t ) + b.left_hand_cube_local_position * t;
            output.left_hand_cube_local_rotation = Quaternion.Slerp( a.left_hand_cube_local_rotation, b.left_hand_cube_local_rotation, t );
        }
        else
        {
            output.left_hand_cube_local_position = a.left_hand_cube_local_position;
            output.left_hand_cube_local_rotation = a.left_hand_cube_local_rotation;
        }

        output.right_hand_holding_cube = a.right_hand_holding_cube;
        output.right_hand_cube_id = a.right_hand_cube_id;
        output.right_hand_authority_sequence = a.right_hand_authority_sequence;
        output.right_hand_ownership_sequence = a.right_hand_ownership_sequence;

        if ( a.right_hand_holding_cube == b.right_hand_holding_cube && a.right_hand_cube_id == b.right_hand_cube_id )
        {
            output.right_hand_cube_local_position = a.right_hand_cube_local_position * ( 1 - t ) + b.right_hand_cube_local_position * t;
            output.right_hand_cube_local_rotation = Quaternion.Slerp( a.right_hand_cube_local_rotation, b.right_hand_cube_local_rotation, t );
        }
        else
        {
            output.right_hand_cube_local_position = a.right_hand_cube_local_position;
            output.right_hand_cube_local_rotation = a.right_hand_cube_local_rotation;
        }
        
        // stream data cannot be interpolated. Use whichever is closer
        if (t > 0.5f)
        {
            output.stream_length = b.stream_length;
            output.stream_data = b.stream_data;
        }
        else
        {
            output.stream_length = a.stream_length;
            output.stream_data = a.stream_data;
        }
    }
};
