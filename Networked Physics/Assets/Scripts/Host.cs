/**
 * Copyright (c) 2017-present, Facebook, Inc.
 * All rights reserved.
 *
 * This source code is licensed under the BSD-style license found in the
 * LICENSE file in the Scripts directory of this source tree. An additional grant 
 * of patent rights can be found in the PATENTS file in the same directory.
 */

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using LiteNetLib;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Assertions;
using Oculus.Platform;
using Oculus.Platform.Models;
using Random = UnityEngine.Random;

public class Host : Common, INetEventListener
{
    public Context context;
    public OVRSpatialAnchor spatialAnchor;

    enum ClientState
    {
        Disconnected,                                   // client is not connected
        Connecting,                                     // client is connecting (joined room, but NAT punched yet)
        Connected                                       // client is fully connected and is sending and receiving packets.
    };

    struct ClientData
    {
        public ClientState state;
        public ulong userId;
        public string oculusId;
        public int anonymousId;
        public double timeConnectionStarted;
        public double timeConnected;
        public double timeLastPacketSent;
        public double timeLastPacketReceived;
        public NetPeer netPeer;

        public void Reset()
        {
            state = ClientState.Disconnected;
            userId = 0;
            oculusId = "";
            timeConnectionStarted = 0.0;
            timeConnected = 0.0f;
            timeLastPacketSent = 0.0;
            timeLastPacketReceived = 0.0;
            netPeer = null;
        }
    };

    ClientData [] client = new ClientData[Constants.MaxClients];
    private bool anchorSaved;

    bool IsClientConnected( int clientIndex )
    {
        Assert.IsTrue( clientIndex >= 0 );
        Assert.IsTrue( clientIndex < Constants.MaxClients );
        return client[clientIndex].state == ClientState.Connected;
    }

    private NetManager netManager;

    void Awake()
    {
        Debug.Log( "*** HOST ***" );

        Assert.IsNotNull( context );

        // IMPORTANT: the host is *always* client 0

        for ( int i = 0; i < Constants.MaxClients; ++i )
            client[i].Reset();

        context.Initialize( 0 );

        context.SetResetSequence( 100 );

        netManager = new NetManager(this);
        netManager.BroadcastReceiveEnabled = true;
        netManager.UnconnectedMessagesEnabled = true;
        
        netManager.Start(Port);
        // send first broadcast
        netManager.SendBroadcast(Array.Empty<byte>(), Port);
        
        if ( !InitializePlatformSDK( GetEntitlementCallback ) )
        {
            SetAnonymousUserConnected();
        }
    }

    protected override void Start()
    {
        base.Start();

        Assert.IsNotNull( context );
        Assert.IsNotNull( localAvatar );

        for ( int i = 0; i < Constants.MaxClients; ++i )
            context.HideRemoteAvatar( i );

        localAvatar.GetComponent<Avatar>().SetContext( context.GetComponent<Context>() );
        localAvatar.transform.position = context.GetRemoteAvatar( 0 ).gameObject.transform.position;
        localAvatar.transform.rotation = context.GetRemoteAvatar( 0 ).gameObject.transform.rotation;
    }

    private void OnDestroy()
    {
        netManager.Stop();
    }

    void GetEntitlementCallback( Message msg )
    {
        if ( !msg.IsError )
        {
            Debug.Log( "You are entitled to use this app" );

            Users.GetLoggedInUser().OnComplete( GetLoggedInUserCallback );
        }
        else
        {
            Debug.Log( "error: You are not entitled to use this app" );
            SetAnonymousUserConnected();
        }
    }

    void GetLoggedInUserCallback( Message<User> msg )
    {
        if ( !msg.IsError )
        {
            Debug.Log( "User id is " + msg.Data.ID );
            Debug.Log( "Oculus id is " + msg.Data.OculusID  );

            SetUserConnected( msg.Data.ID, msg.Data.OculusID );
            spatialAnchor.Save(new OVRSpatialAnchor.SaveOptions() { Storage = OVRSpace.StorageLocation.Cloud }, OnSpatialAnchorSaved );
        }
        else
        {
            Debug.Log( "error: Could not get signed in user" );
            SetAnonymousUserConnected();
        }
    }

    private void OnSpatialAnchorSaved(OVRSpatialAnchor anchor, bool saved)
    {
        if (saved)
        {
            anchorSaved = true;
            ShareSpatialAnchor();
        }
    }

    private void SetAnonymousUserConnected()
    {
        // Continue anyway with 0 ID for local testing
        SetUserConnected( 0, "Host" );
    }

    private void SetUserConnected( ulong userId, string oculusId )
    {
        client[0].state = ClientState.Connected;
        client[0].userId = userId;
        client[0].oculusId = oculusId;
        client[0].anonymousId = Random.Range( 0, int.MaxValue );
        localAvatar.GetComponent<Avatar>().LoadAvatar( client[0].userId, client[0].anonymousId );
    }

    int FindClientByNetPeer( NetPeer netPeer )
    {
        for ( int i = 1; i < Constants.MaxClients; ++i )
        {
            if ( client[i].state != ClientState.Disconnected && client[i].netPeer == netPeer )
                return i;
        }
        return -1;
    }

    int FindFreeClientIndex()
    {
        for ( int i = 1; i < Constants.MaxClients; ++i )
        {
            if ( client[i].state == ClientState.Disconnected )
                return i;
        }
        return -1;
    }

    public void OnConnectionRequest(ConnectionRequest request)
    {
        if (request.Data.TryGetULong(out ulong userId) &&
            request.Data.TryGetString(out string oculusId) &&
            request.Data.TryGetInt(out int anonymousId))
        {
            int clientIndex = FindFreeClientIndex();
            if (clientIndex != -1)
            {
                StartClientConnection(clientIndex, userId, oculusId, anonymousId, request.Accept());
                return;
            }
        }

        request.Reject();
    }

    void StartClientConnection( int clientIndex, ulong userId, string oculusId, int anonymousId, NetPeer netPeer )
    {
        Debug.Log( "Starting connection to client " + oculusId + " [" + userId + "]" );

        Assert.IsTrue( clientIndex != 0 );

        if ( client[clientIndex].state != ClientState.Disconnected )
            DisconnectClient( clientIndex );

        client[clientIndex].state = ClientState.Connecting;
        client[clientIndex].oculusId = oculusId;
        client[clientIndex].userId = userId;
        client[clientIndex].anonymousId = anonymousId;
        client[clientIndex].timeConnectionStarted = renderTime;
        client[clientIndex].netPeer = netPeer;
    }

    void ConnectClient( int clientIndex )
    {
        Assert.IsTrue( clientIndex != 0 );

        if ( client[clientIndex].state != ClientState.Connecting )
            return;

        client[clientIndex].state = ClientState.Connected;
        client[clientIndex].timeConnected = renderTime;
        client[clientIndex].timeLastPacketSent = renderTime;
        client[clientIndex].timeLastPacketReceived = renderTime;

        OnClientConnect( clientIndex );

        BroadcastServerInfo();
    }

    void DisconnectClient( int clientIndex )
    {
        Assert.IsTrue( clientIndex != 0 );
        Assert.IsTrue( IsClientConnected( clientIndex ) );

        OnClientDisconnect( clientIndex );

        netManager.DisconnectPeer( client[clientIndex].netPeer );

        client[clientIndex].Reset();

        BroadcastServerInfo();
    }

    void OnClientConnect( int clientIndex )
    {
        Debug.Log( client[clientIndex].oculusId + " joined the game as client " + clientIndex );

        ShareSpatialAnchor();
        context.ShowRemoteAvatar( clientIndex, client[clientIndex].userId, client[clientIndex].anonymousId );
    }

    void OnClientDisconnect( int clientIndex )
    {
        Debug.Log( client[clientIndex].oculusId + " left the game" );
        
        context.HideRemoteAvatar( clientIndex );

        context.ResetAuthorityForClientCubes( clientIndex );

        context.GetServerConnectionData( clientIndex ).Reset();
    }

    public void OnPeerConnected( NetPeer peer )
    {
        int clientIndex = FindClientByNetPeer( peer );

        if ( clientIndex != -1 )
        {
            ConnectClient( clientIndex );
        }
    }
    
    public void OnPeerDisconnected( NetPeer peer, DisconnectInfo disconnectInfo )
    {
        int clientIndex = FindClientByNetPeer( peer );

        if (clientIndex != -1)
        {
            DisconnectClient(clientIndex);
        }
    }

    bool readyToShutdown = false;

    protected override void OnQuit()
    {
        for ( int i = 1; i < Constants.MaxClients; ++i )
        {
            if ( IsClientConnected( i ) )
                DisconnectClient( i );
        }
    }

    protected override bool ReadyToShutdown()
    {
        return readyToShutdown;
    }
    
    new void Update()
    {
        base.Update();

        // apply host avatar per-remote client at render time with interpolation

        for ( int i = 1; i < Constants.MaxClients; ++i )
        {
            if ( client[i].state != ClientState.Connected )
                continue;

            Context.ConnectionData connectionData = context.GetServerConnectionData( i );

            int fromClientIndex = i;
            int toClientIndex = 0;

            int numInterpolatedAvatarStates;
            ushort avatarResetSequence;
            if ( connectionData.jitterBuffer.GetInterpolatedAvatarState( ref interpolatedAvatarState, out numInterpolatedAvatarStates, out avatarResetSequence ) )
            {
                if ( avatarResetSequence == context.GetResetSequence() )
                {
                    context.ApplyAvatarStateUpdates( numInterpolatedAvatarStates, ref interpolatedAvatarState, fromClientIndex, toClientIndex );
                }
            }
        }

        // advance jitter buffer time

        for ( int i = 1; i < Constants.MaxClients; ++i )
        {
            if ( client[i].state == ClientState.Connected )
            {
                context.GetServerConnectionData( i ).jitterBuffer.AdvanceTime( Time.deltaTime );
            }
        }

        // check for timeouts

        CheckForTimeouts();
    }

    new void FixedUpdate()
    {
        var avatar = localAvatar.GetComponent<Avatar>();

        bool reset = Input.GetKey( "space" ) || ( avatar.IsPressingIndex() && avatar.IsPressingX() );

        if ( reset )
        {
            context.Reset();
            context.IncreaseResetSequence();
        }

        context.CheckForAtRestObjects();

        ProcessPacketsFromConnectedPeers();

        SendPacketsToConnectedClients();

        context.CheckForAtRestObjects();

        base.FixedUpdate();
    }

    void CheckForTimeouts()
    {
        for ( int i = 1; i < Constants.MaxClients; ++i )
        {
            if ( client[i].state == ClientState.Connecting )
            {
                if ( client[i].timeConnectionStarted + ConnectionTimeout < renderTime )
                {
                    Debug.Log( "Client " + i + " timed out while connecting" );

                    DisconnectClient( i );
                }
            }
            else if ( client[i].state == ClientState.Connected )
            {
                if ( client[i].timeLastPacketReceived + ConnectionTimeout < renderTime )
                {
                    Debug.Log( "Client " + i + " timed out" );

                    DisconnectClient( i );
                }
            }
        }
    }

    public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod)
    {
        int clientIndex = FindClientByNetPeer(peer);
        if (clientIndex == -1)
            return;

        if (!IsClientConnected(clientIndex))
            return;

        byte packetType = reader.PeekByte();

        if (packetType == (byte)PacketSerializer.PacketType.StateUpdate)
        {
            if (enableJitterBuffer)
            {
                AddStateUpdatePacketToJitterBuffer(context, context.GetServerConnectionData(clientIndex), reader.RawData, reader.Position);
            }
            else
            {
                ProcessStateUpdatePacket(reader.RawData, reader.Position, clientIndex);
            }
        }

        client[clientIndex].timeLastPacketReceived = renderTime;
    }
    
    public void ProcessPacketsFromConnectedPeers()
    {
        netManager.PollEvents();
        ProcessAcks();

        // process client state update from jitter buffer

        if ( enableJitterBuffer )
        {
            for ( int i = 1; i < Constants.MaxClients; ++i )
            {
                if ( client[i].state == ClientState.Connected )
                {
                    ProcessStateUpdateFromJitterBuffer( context, context.GetServerConnectionData( i ), i, 0, enableJitterBuffer );
                }
            }
        }

        // advance remote frame number

        for ( int i = 1; i < Constants.MaxClients; ++i )
        {
            if ( client[i].state == ClientState.Connected )
            {
                Context.ConnectionData connectionData = context.GetServerConnectionData( i );

                if ( !connectionData.firstRemotePacket )
                    connectionData.remoteFrameNumber++;
            }
        }
    }

    void SendPacketsToConnectedClients()
    {
        for ( int clientIndex = 1; clientIndex < Constants.MaxClients; ++clientIndex )
        {
            if ( !IsClientConnected( clientIndex ) )
                continue;

            Context.ConnectionData connectionData = context.GetServerConnectionData( clientIndex );

            byte[] packetData = GenerateStateUpdatePacket( connectionData, clientIndex, (float) ( physicsTime - renderTime ) );

            client[clientIndex].netPeer.Send( packetData, DeliveryMethod.ReliableUnordered );

            client[clientIndex].timeLastPacketSent = renderTime;
        }
    }

    public void BroadcastServerInfo()
    {
        byte[] packetData = GenerateServerInfoPacket();

        for ( int clientIndex = 1; clientIndex < Constants.MaxClients; ++clientIndex )
        {
            if ( !IsClientConnected( clientIndex ) )
                continue;

            client[clientIndex].netPeer.Send( packetData, DeliveryMethod.ReliableUnordered );

            client[clientIndex].timeLastPacketSent = renderTime;
        }
    }

    protected void ShareSpatialAnchor()
    {
        if (!anchorSaved)
            return;
        
        var anchorUuid = spatialAnchor.Uuid;
        List<OVRSpaceUser> spaceUsers = new List<OVRSpaceUser>();
        for (int i = 1; i < Constants.MaxClients; i++)
        {
            if ( IsClientConnected(i) && client[i].userId != 0 && client[i].userId != client[0].userId )
            {
                spaceUsers.Add( new OVRSpaceUser( client[i].userId ) );
            }
        }

        spatialAnchor.Share( spaceUsers, res =>
        {
            if (res != OVRSpatialAnchor.OperationResult.Success) return;
            
            byte[] packet = new byte[17];
            packet[0] = (byte)PacketSerializer.PacketType.AnchorGuid;
            anchorUuid.TryWriteBytes(packet.AsSpan(1));
                
            for ( int i = 1; i < Constants.MaxClients; i++ )
            {
                if ( IsClientConnected( i ) && client[i].userId != 0 )
                {
                    client[i].netPeer.Send( packet, DeliveryMethod.ReliableUnordered );
                }
            }
        });
    }
   
    public byte[] GenerateServerInfoPacket()
    {
        for ( int i = 0; i < Constants.MaxClients; ++i )
        {
            if ( IsClientConnected( i ) )
            {
                serverInfo.clientConnected[i] = true;
                serverInfo.clientUserId[i] = client[i].userId;
                serverInfo.clientUserName[i] = client[i].oculusId;
                serverInfo.clientAnonymousId[i] = client[i].anonymousId;
            }
            else
            {
                serverInfo.clientConnected[i] = false;
                serverInfo.clientUserId[i] = 0;
                serverInfo.clientUserName[i] = "";
                serverInfo.clientAnonymousId[i] = 0;
            }
        }

        WriteServerInfoPacket( serverInfo.clientConnected, serverInfo.clientUserId, serverInfo.clientUserName, serverInfo.clientAnonymousId );

        byte[] packetData = writeStream.GetData();

        return packetData;
    }

    public byte[] GenerateStateUpdatePacket( Context.ConnectionData connectionData, int toClientIndex, float avatarSampleTimeOffset )
    {
        int maxStateUpdates = Math.Min( Constants.NumCubes, Constants.MaxStateUpdates );

        int numStateUpdates = maxStateUpdates;

        context.UpdateCubePriority();

        context.GetMostImportantCubeStateUpdates( connectionData, ref numStateUpdates, ref cubeIds, ref cubeState );

        Network.PacketHeader writePacketHeader;

        connectionData.connection.GeneratePacketHeader( out writePacketHeader );

        writePacketHeader.resetSequence = context.GetResetSequence();

        writePacketHeader.frameNumber = (uint) frameNumber;

        writePacketHeader.avatarSampleTimeOffset = avatarSampleTimeOffset;

        DetermineNotChangedAndDeltas( context, connectionData, writePacketHeader.sequence, numStateUpdates, ref cubeIds, ref notChanged, ref hasDelta, ref baselineSequence, ref cubeState, ref cubeDelta );

        DeterminePrediction( context, connectionData, writePacketHeader.sequence, numStateUpdates, ref cubeIds, ref notChanged, ref hasDelta, ref perfectPrediction, ref hasPredictionDelta, ref baselineSequence, ref cubeState, ref predictionDelta );

        int numAvatarStates = 0;

        numAvatarStates = 0;

        for ( int i = 0; i < Constants.MaxClients; ++i )
        {
            if ( i == toClientIndex )
                continue;

            if ( i == 0 )
            {
                // grab state from the local avatar.

                localAvatar.GetComponent<Avatar>().GetAvatarState( out avatarState[numAvatarStates] );
                AvatarState.Quantize( ref avatarState[numAvatarStates], out avatarStateQuantized[numAvatarStates] );
                numAvatarStates++;
            }
            else
            {
                // grab state from a remote avatar.

                var remoteAvatar = context.GetRemoteAvatar( i );

                if ( remoteAvatar )
                {
                    remoteAvatar.GetAvatarState( out avatarState[numAvatarStates] );
                    AvatarState.Quantize( ref avatarState[numAvatarStates], out avatarStateQuantized[numAvatarStates] );
                    numAvatarStates++;
                }
            }
        }

        WriteStateUpdatePacket( ref writePacketHeader, numAvatarStates, ref avatarStateQuantized, numStateUpdates, ref cubeIds, ref notChanged, ref hasDelta, ref perfectPrediction, ref hasPredictionDelta, ref baselineSequence, ref cubeState, ref cubeDelta, ref predictionDelta );

        byte[] packetData = writeStream.GetData();

        // add the sent cube states to the send delta buffer

        AddPacketToDeltaBuffer( ref connectionData.sendDeltaBuffer, writePacketHeader.sequence, context.GetResetSequence(), numStateUpdates, ref cubeIds, ref cubeState );

        // reset cube priority for the cubes that were included in the packet (so other cubes have a chance to be sent...)

        context.ResetCubePriority( connectionData, numStateUpdates, cubeIds );

        return packetData;
    }

    public void ProcessStateUpdatePacket( byte[] packetData, int offset, int fromClientIndex )
    {
        int readNumAvatarStates = 0;
        int readNumStateUpdates = 0;

        Context.ConnectionData connectionData = context.GetServerConnectionData( fromClientIndex );

        Network.PacketHeader readPacketHeader;

        if ( ReadStateUpdatePacket( packetData,  offset, out readPacketHeader, out readNumAvatarStates, ref readAvatarStateQuantized, out readNumStateUpdates, ref readCubeIds, ref readNotChanged, ref readHasDelta, ref readPerfectPrediction, ref readHasPredictionDelta, ref readBaselineSequence, ref readCubeState, ref readCubeDelta, ref readPredictionDelta ) )
        {
            // unquantize avatar states

            for ( int i = 0; i < readNumAvatarStates; ++i )
                AvatarState.Unquantize( ref readAvatarStateQuantized[i], out readAvatarState[i] );

            // ignore any updates from a client with a different reset sequence #

            if ( context.GetResetSequence() != readPacketHeader.resetSequence )
                return;

            // decode the predicted cube states from baselines

            DecodePrediction( connectionData.receiveDeltaBuffer, readPacketHeader.sequence, context.GetResetSequence(), readNumStateUpdates, ref readCubeIds, ref readPerfectPrediction, ref readHasPredictionDelta, ref readBaselineSequence, ref readCubeState, ref readPredictionDelta );

            // decode the not changed and delta cube states from baselines

            DecodeNotChangedAndDeltas( connectionData.receiveDeltaBuffer, context.GetResetSequence(), readNumStateUpdates, ref readCubeIds, ref readNotChanged, ref readHasDelta, ref readBaselineSequence, ref readCubeState, ref readCubeDelta );

            // add the cube states to the receive delta buffer

            AddPacketToDeltaBuffer( ref connectionData.receiveDeltaBuffer, readPacketHeader.sequence, context.GetResetSequence(), readNumStateUpdates, ref readCubeIds, ref readCubeState );

            // apply the state updates to cubes

            context.ApplyCubeStateUpdates( readNumStateUpdates, ref readCubeIds, ref readCubeState, fromClientIndex, 0, enableJitterBuffer );

            // apply avatar state updates

            context.ApplyAvatarStateUpdates( readNumAvatarStates, ref readAvatarState, fromClientIndex, 0 );

            // process the packet header

            connectionData.connection.ProcessPacketHeader( ref readPacketHeader );
        }                            
    }

    void ProcessAcks()
    {
        Profiler.BeginSample( "Process Acks" );
        {
            for ( int clientIndex = 1; clientIndex < Constants.MaxClients; ++clientIndex )
            {
                for ( int i = 1; i < Constants.MaxClients; ++i )
                {
                    Context.ConnectionData connectionData = context.GetServerConnectionData( i );

                    ProcessAcksForConnection( context, connectionData );
                }
            }
        }

        Profiler.EndSample();
    }

    public void OnNetworkError( IPEndPoint endPoint, SocketError socketError )
    {
        Debug.LogError( "Network Error! " + socketError );
    }

    public void OnNetworkReceiveUnconnected( IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType )
    {
        if (reader.AvailableBytes < MagicClientBytes.Length)
            return;
        bool equal = true;
        // compare all bytes for constant time comparison
        for (int i = 0; i < MagicClientBytes.Length; i++)
        {
            equal &= reader.GetByte() == MagicClientBytes[i];
        }

        if (!equal)
            return;
        
        Debug.Log("Received Broadcast from a client to retrieve host information!");
        netManager.SendUnconnectedMessage(MagicHostBytes, remoteEndPoint);
    }

    public void OnNetworkLatencyUpdate( NetPeer peer, int latency )
    {
        // DO nothing
    }
}
