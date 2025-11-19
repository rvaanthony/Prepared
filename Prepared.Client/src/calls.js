// Calls dashboard - Real-time call monitoring with Google Maps
import './layout.js';
import * as signalR from '@microsoft/signalr';

// Bootstrap is loaded via script tag in _Layout.cshtml, so it's available globally

/**
 * Call Dashboard Application
 * Manages real-time call monitoring, transcription, and map visualization
 */
class CallDashboard {
    constructor() {
        this.connection = null;
        this.map = null;
        this.markers = new Map(); // CallSid -> Google Maps Marker
        this.activeCalls = new Map(); // CallSid -> Call data
        this.currentCallSid = null;
        
        // UI elements
        this.transcriptContainer = null;
        this.summaryContainer = null;
        this.callsListContainer = null;
        this.statusIndicator = null;
        
        // Google Maps API key (injected from server)
        this.googleMapsApiKey = window.googleMapsApiKey || '';
        this.signalRHubUrl = window.signalRHubUrl || '/hubs/transcript';
        
        this.init();
    }

    /**
     * Initialize the dashboard
     */
    async init() {
        console.log('Initializing Call Dashboard...');
        
        // Get UI elements
        this.transcriptContainer = document.getElementById('transcriptContainer');
        this.summaryContainer = document.getElementById('summaryContainer');
        this.callsListContainer = document.getElementById('callsList');
        this.statusIndicator = document.getElementById('connectionStatus');
        
        // Initialize Google Maps
        await this.initGoogleMaps();
        
        // Initialize SignalR connection
        await this.initSignalR();
        
        // Set up event listeners
        this.setupEventListeners();
        
        console.log('Call Dashboard initialized');
    }

    /**
     * Initialize Google Maps
     */
    async initGoogleMaps() {
        if (!this.googleMapsApiKey) {
            console.warn('Google Maps API key not configured');
            const mapContainer = document.getElementById('map');
            if (mapContainer) {
                mapContainer.innerHTML = '<div class="alert alert-warning">Google Maps API key not configured. Please set it in appsettings.json</div>';
            }
            return;
        }

        // Load Google Maps script dynamically
        return new Promise((resolve, reject) => {
            if (window.google && window.google.maps) {
                this.createMap();
                resolve();
                return;
            }

            const script = document.createElement('script');
            script.src = `https://maps.googleapis.com/maps/api/js?key=${this.googleMapsApiKey}&libraries=places`;
            script.async = true;
            script.defer = true;
            script.onload = () => {
                this.createMap();
                resolve();
            };
            script.onerror = () => {
                console.error('Failed to load Google Maps API');
                reject(new Error('Failed to load Google Maps API'));
            };
            document.head.appendChild(script);
        });
    }

    /**
     * Create the Google Maps instance
     */
    createMap() {
        const mapContainer = document.getElementById('map');
        if (!mapContainer) {
            console.error('Map container not found');
            return;
        }

        // Default center (can be configured)
        const defaultCenter = { lat: 37.7749, lng: -122.4194 }; // San Francisco

        this.map = new google.maps.Map(mapContainer, {
            center: defaultCenter,
            zoom: 10,
            mapTypeControl: true,
            streetViewControl: true,
            fullscreenControl: true,
            zoomControl: true
        });

        console.log('Google Maps initialized');
    }

    /**
     * Initialize SignalR connection
     */
    async initSignalR() {
        try {
            this.connection = new signalR.HubConnectionBuilder()
                .withUrl(this.signalRHubUrl)
                .withAutomaticReconnect({
                    nextRetryDelayInMilliseconds: retryContext => {
                        // Exponential backoff: 0s, 2s, 10s, 30s, then 30s intervals
                        if (retryContext.previousRetryCount === 0) return 0;
                        if (retryContext.previousRetryCount === 1) return 2000;
                        if (retryContext.previousRetryCount === 2) return 10000;
                        return 30000;
                    }
                })
                .configureLogging(signalR.LogLevel.Information)
                .build();

            // Set up event handlers
            this.setupSignalRHandlers();

            // Start connection
            await this.connection.start();
            this.updateConnectionStatus('connected');
            console.log('SignalR connected');

        } catch (error) {
            console.error('SignalR connection error:', error);
            this.updateConnectionStatus('disconnected');
        }
    }

    /**
     * Set up SignalR event handlers
     */
    setupSignalRHandlers() {
        // Connection state changes
        this.connection.onreconnecting(() => {
            this.updateConnectionStatus('reconnecting');
            console.log('SignalR reconnecting...');
        });

        this.connection.onreconnected(() => {
            this.updateConnectionStatus('connected');
            console.log('SignalR reconnected');
        });

        this.connection.onclose(() => {
            this.updateConnectionStatus('disconnected');
            console.log('SignalR connection closed');
        });

        // Transcript updates
        this.connection.on('ReceiveTranscriptUpdate', (callSid, transcript, isFinal) => {
            this.handleTranscriptUpdate(callSid, transcript, isFinal);
        });

        // Call status updates
        this.connection.on('ReceiveCallStatusUpdate', (callSid, status) => {
            this.handleCallStatusUpdate(callSid, status);
        });

        // Location updates
        this.connection.on('ReceiveLocationUpdate', (callSid, latitude, longitude, address) => {
            this.handleLocationUpdate(callSid, latitude, longitude, address);
        });

        // Summary updates
        this.connection.on('ReceiveSummaryUpdate', (callSid, summary, keyFindings) => {
            this.handleSummaryUpdate(callSid, summary, keyFindings);
        });
    }

    /**
     * Handle transcript updates
     */
    handleTranscriptUpdate(callSid, transcript, isFinal) {
        console.log('Transcript update:', { callSid, transcript, isFinal });

        // Update active calls data
        if (!this.activeCalls.has(callSid)) {
            this.activeCalls.set(callSid, {
                callSid,
                transcript: '',
                summary: null,
                location: null,
                status: 'unknown',
                startedAt: new Date()
            });
        }

        const call = this.activeCalls.get(callSid);
        call.transcript += (call.transcript ? ' ' : '') + transcript;
        call.lastUpdate = new Date();

        // Update UI if this is the current call
        if (this.currentCallSid === callSid) {
            this.updateTranscriptDisplay(call.transcript, isFinal);
        }

        // Update calls list
        this.updateCallsList();
    }

    /**
     * Handle call status updates
     */
    handleCallStatusUpdate(callSid, status) {
        console.log('Call status update:', { callSid, status });

        if (!this.activeCalls.has(callSid)) {
            this.activeCalls.set(callSid, {
                callSid,
                transcript: '',
                summary: null,
                location: null,
                status: status,
                startedAt: new Date()
            });
        }

        const call = this.activeCalls.get(callSid);
        call.status = status;

        // Join the SignalR group for this call
        if (this.connection && this.connection.state === signalR.HubConnectionState.Connected) {
            this.connection.invoke('JoinCallGroup', callSid).catch(err => {
                console.error('Error joining call group:', err);
            });
        }

        // Update UI
        this.updateCallsList();

        // If this is a new call, select it
        if (status === 'ringing' || status === 'in-progress') {
            this.selectCall(callSid);
        }
    }

    /**
     * Handle location updates
     */
    handleLocationUpdate(callSid, latitude, longitude, address) {
        console.log('Location update:', { callSid, latitude, longitude, address });

        if (!this.activeCalls.has(callSid)) {
            return;
        }

        const call = this.activeCalls.get(callSid);
        call.location = { latitude, longitude, address };
        call.lastUpdate = new Date();

        // Add or update marker on map
        this.updateMapMarker(callSid, latitude, longitude, address);

        // Update UI if this is the current call
        if (this.currentCallSid === callSid) {
            this.updateLocationDisplay(latitude, longitude, address);
        }

        // Update calls list
        this.updateCallsList();
    }

    /**
     * Handle summary updates
     */
    handleSummaryUpdate(callSid, summary, keyFindings) {
        console.log('Summary update:', { callSid, summary, keyFindings });

        if (!this.activeCalls.has(callSid)) {
            return;
        }

        const call = this.activeCalls.get(callSid);
        call.summary = { summary, keyFindings };
        call.lastUpdate = new Date();

        // Update UI if this is the current call
        if (this.currentCallSid === callSid) {
            this.updateSummaryDisplay(summary, keyFindings);
        }

        // Update calls list
        this.updateCallsList();
    }

    /**
     * Update map marker for a call
     */
    updateMapMarker(callSid, latitude, longitude, address) {
        if (!this.map) return;

        // Remove existing marker if any
        if (this.markers.has(callSid)) {
            this.markers.get(callSid).setMap(null);
        }

        // Create new marker
        const marker = new google.maps.Marker({
            position: { lat: latitude, lng: longitude },
            map: this.map,
            title: address || `Call ${callSid}`,
            animation: google.maps.Animation.DROP
        });

        // Create info window
        const infoWindow = new google.maps.InfoWindow({
            content: `
                <div style="padding: 8px;">
                    <strong>Call: ${callSid.substring(0, 12)}...</strong><br/>
                    ${address ? `<div>${address}</div>` : ''}
                    <div style="margin-top: 4px; font-size: 0.9em; color: #666;">
                        ${latitude.toFixed(6)}, ${longitude.toFixed(6)}
                    </div>
                </div>
            `
        });

        marker.addListener('click', () => {
            infoWindow.open(this.map, marker);
            this.selectCall(callSid);
        });

        this.markers.set(callSid, marker);

        // Center map on marker if it's the current call
        if (this.currentCallSid === callSid) {
            this.map.setCenter({ lat: latitude, lng: longitude });
            this.map.setZoom(15);
        }
    }

    /**
     * Select a call to display details
     */
    selectCall(callSid) {
        this.currentCallSid = callSid;
        const call = this.activeCalls.get(callSid);

        if (!call) return;

        // Update transcript display
        this.updateTranscriptDisplay(call.transcript || '', false);

        // Update summary display
        if (call.summary) {
            this.updateSummaryDisplay(call.summary.summary, call.summary.keyFindings);
        } else {
            this.updateSummaryDisplay('', []);
        }

        // Update location display
        if (call.location) {
            this.updateLocationDisplay(
                call.location.latitude,
                call.location.longitude,
                call.location.address
            );
        }

        // Center map on location if available
        if (call.location && this.map) {
            this.map.setCenter({
                lat: call.location.latitude,
                lng: call.location.longitude
            });
            this.map.setZoom(15);
        }

        // Update active call highlight in list
        this.updateCallsList();
    }

    /**
     * Update transcript display
     */
    updateTranscriptDisplay(transcript, isFinal) {
        if (!this.transcriptContainer) return;

        const finalClass = isFinal ? 'text-muted' : 'text-primary';
        const finalIndicator = isFinal ? '' : '<span class="badge bg-info ms-2">Live</span>';

        this.transcriptContainer.innerHTML = `
            <div class="transcript-content ${finalClass}">
                ${this.escapeHtml(transcript || 'No transcript yet...')}
                ${finalIndicator}
            </div>
        `;

        // Auto-scroll to bottom
        this.transcriptContainer.scrollTop = this.transcriptContainer.scrollHeight;
    }

    /**
     * Update summary display
     */
    updateSummaryDisplay(summary, keyFindings) {
        if (!this.summaryContainer) return;

        if (!summary) {
            this.summaryContainer.innerHTML = '<div class="text-muted">Summary will appear here when available...</div>';
            return;
        }

        let html = `<div class="summary-content"><p>${this.escapeHtml(summary)}</p>`;

        if (keyFindings && keyFindings.length > 0) {
            html += '<h6 class="mt-3 mb-2">Key Findings:</h6><ul class="list-unstyled">';
            keyFindings.forEach(finding => {
                html += `<li class="mb-1"><span class="text-success me-2">✓</span>${this.escapeHtml(finding)}</li>`;
            });
            html += '</ul>';
        }

        html += '</div>';
        this.summaryContainer.innerHTML = html;
    }

    /**
     * Update location display
     */
    updateLocationDisplay(latitude, longitude, address) {
        const locationContainer = document.getElementById('locationDisplay');
        if (!locationContainer) return;

        locationContainer.innerHTML = `
            <div class="location-info">
                ${address ? `<div class="fw-bold">${this.escapeHtml(address)}</div>` : ''}
                <div class="text-muted small">
                    ${latitude.toFixed(6)}, ${longitude.toFixed(6)}
                </div>
            </div>
        `;
    }

    /**
     * Update calls list
     */
    updateCallsList() {
        if (!this.callsListContainer) return;

        const calls = Array.from(this.activeCalls.values())
            .sort((a, b) => (b.lastUpdate || b.startedAt) - (a.lastUpdate || a.startedAt));

        if (calls.length === 0) {
            this.callsListContainer.innerHTML = '<div class="text-muted p-3">No active calls</div>';
            return;
        }

        let html = '<div class="list-group">';
        calls.forEach(call => {
            const isActive = this.currentCallSid === call.callSid;
            const activeClass = isActive ? 'active' : '';
            const statusBadge = this.getStatusBadge(call.status);
            const timeAgo = call.lastUpdate 
                ? window.Prepared.formatRelativeTime(call.lastUpdate)
                : window.Prepared.formatRelativeTime(call.startedAt);

            html += `
                <a href="#" class="list-group-item list-group-item-action ${activeClass}" 
                   data-call-sid="${call.callSid}" 
                   onclick="window.callDashboard.selectCall('${call.callSid}'); return false;">
                    <div class="d-flex w-100 justify-content-between">
                        <h6 class="mb-1">${call.callSid.substring(0, 12)}...</h6>
                        ${statusBadge}
                    </div>
                    <p class="mb-1 small text-muted">
                        ${call.transcript ? call.transcript.substring(0, 100) + '...' : 'No transcript yet'}
                    </p>
                    <small class="text-muted">${timeAgo}</small>
                </a>
            `;
        });
        html += '</div>';

        this.callsListContainer.innerHTML = html;
    }

    /**
     * Get status badge HTML
     */
    getStatusBadge(status) {
        const badges = {
            'ringing': '<span class="badge bg-warning">Ringing</span>',
            'in-progress': '<span class="badge bg-primary">In Progress</span>',
            'stream_started': '<span class="badge bg-info">Streaming</span>',
            'stream_stopped': '<span class="badge bg-secondary">Stopped</span>',
            'completed': '<span class="badge bg-success">Completed</span>',
            'failed': '<span class="badge bg-danger">Failed</span>'
        };
        return badges[status] || '<span class="badge bg-secondary">' + status + '</span>';
    }

    /**
     * Update connection status indicator
     */
    updateConnectionStatus(status) {
        if (!this.statusIndicator) return;

        const statusClasses = {
            'connected': 'bg-success',
            'reconnecting': 'bg-warning',
            'disconnected': 'bg-danger'
        };

        const statusText = {
            'connected': 'Connected',
            'reconnecting': 'Reconnecting...',
            'disconnected': 'Disconnected'
        };

        this.statusIndicator.className = `badge connection-badge ${statusClasses[status] || 'bg-secondary'} fs-6 px-3 py-2`;
        this.statusIndicator.textContent = statusText[status] || status;
    }

    /**
     * Set up event listeners
     */
    setupEventListeners() {
        // Refresh button
        const refreshBtn = document.getElementById('refreshCalls');
        if (refreshBtn) {
            refreshBtn.addEventListener('click', () => {
                this.updateCallsList();
            });
        }
    }

    /**
     * Escape HTML to prevent XSS
     */
    escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }
}

// Initialize dashboard when DOM is ready
document.addEventListener('DOMContentLoaded', () => {
    window.callDashboard = new CallDashboard();
});

