// Helper function to check if Leaflet is ready
window.isLeafletReady = function() {
    return typeof L !== 'undefined';
};

// Member Map Initialization
window.initializeMemberMap = function (mapDataJson) {
    try {
        // Check if Leaflet is loaded
        if (typeof L === 'undefined') {
            console.error('Leaflet library is not loaded');
            return false;
        }

        const cityGroups = JSON.parse(mapDataJson);

        // Check if map container exists
        const mapContainer = document.getElementById('map');
        if (!mapContainer) {
            console.error('Map container #map not found');
            return false;
        }
        
        // Check if container has dimensions
        const rect = mapContainer.getBoundingClientRect();
        if (rect.width === 0 || rect.height === 0) {
            console.error('Map container has no dimensions:', rect);
            return false;
        }
        
        console.log('Map container dimensions:', rect.width, 'x', rect.height);

        // Remove existing map instance if any
        if (mapContainer._leaflet_id) {
            try {
                // Try to remove the map properly
                const existingMap = mapContainer._leaflet_map;
                if (existingMap) {
                    existingMap.remove();
                }
            } catch (e) {
                console.warn('Error removing existing map:', e);
            }
            mapContainer._leaflet_id = null;
            mapContainer.innerHTML = '';
        }

        // Calculate center point (average of all coordinates) or default to Portugal
        let centerLat = 39.5; // Center of Portugal
        let centerLng = -8.0;
        let initialZoom = 7;

        if (cityGroups && cityGroups.length > 0) {
            centerLat = cityGroups.reduce((sum, city) => sum + city.Latitude, 0) / cityGroups.length;
            centerLng = cityGroups.reduce((sum, city) => sum + city.Longitude, 0) / cityGroups.length;
        } else {
            console.log('No city data available, initializing map with default center (Portugal)');
        }

        // Initialize map centered on calculated center or Portugal
        const map = L.map('map', {
            preferCanvas: true,
            zoomControl: true,
            attributionControl: true
        }).setView([centerLat, centerLng], initialZoom);
        
        // Store map reference
        mapContainer._leaflet_map = map;

        // Add tile layer (using CartoDB Dark Matter for dark theme)
        console.log('Adding tile layer to map...');
        const tileLayer = L.tileLayer('https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png', {
            attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors &copy; <a href="https://carto.com/attributions">CARTO</a>',
            subdomains: 'abcd',
            maxZoom: 19
        });
        
        tileLayer.on('loading', function() {
            console.log('Tiles are loading...');
        });
        
        tileLayer.on('load', function() {
            console.log('Tiles loaded successfully');
        });
        
        tileLayer.on('tileerror', function(error) {
            console.error('Tile loading error:', error);
        });
        
        tileLayer.addTo(map);
        
        // Invalidate size immediately to ensure proper rendering
        map.invalidateSize();
        console.log('Map size invalidated');

        // Add markers for each city (if any)
        if (cityGroups && cityGroups.length > 0) {
            cityGroups.forEach(cityGroup => {
                // Create custom icon with member count
                const markerIcon = L.divIcon({
                    className: 'custom-marker',
                    html: `<div class="marker-pin" style="background: linear-gradient(135deg, #8a2be2 0%, #6a1bb2 100%); width: 30px; height: 30px; border-radius: 50% 50% 50% 0; position: relative; transform: rotate(-45deg); border: 3px solid #fff; box-shadow: 0 2px 8px rgba(0,0,0,0.4);"></div>
                       <div class="marker-count" style="position: absolute; top: 5px; left: 50%; transform: translateX(-50%) rotate(45deg); color: white; font-weight: bold; font-size: 12px; text-shadow: 0 1px 2px rgba(0,0,0,0.8);">${cityGroup.MemberCount}</div>`,
                    iconSize: [30, 42],
                    iconAnchor: [15, 42],
                    popupAnchor: [0, -42]
                });

                const marker = L.marker([cityGroup.Latitude, cityGroup.Longitude], { 
                    icon: markerIcon 
                }).addTo(map);

                // Create popup content with limited member list for performance
                const maxMembersToShow = 10;
                const membersToShow = cityGroup.Members.slice(0, maxMembersToShow);
                const hasMoreMembers = cityGroup.Members.length > maxMembersToShow;
                
                let popupContent = `
                    <div class="popup-city-name">${cityGroup.CityName}</div>
                    <div class="popup-member-count">
                        <i class="bi bi-people-fill"></i> ${cityGroup.MemberCount} ${cityGroup.MemberCount === 1 ? 'membro' : 'membros'}
                    </div>
                    <div class="popup-members-list">
                `;

                membersToShow.forEach(member => {
                    popupContent += `
                        <div class="popup-member-item">
                            <img src="${member.ImageUrl}" alt="${member.FullName}" class="popup-member-avatar" onerror="this.src='/images/default-avatar.webp';" />
                            <div class="popup-member-info">
                                <p class="popup-member-name">${member.FullName}</p>
                                <p class="popup-member-nickname">${member.Nickname}</p>
                            </div>
                        </div>
                    `;
                });
                
                if (hasMoreMembers) {
                    const remainingCount = cityGroup.Members.length - maxMembersToShow;
                    popupContent += `
                        <div class="popup-member-item" style="text-align: center; font-style: italic; color: #b0b0b0;">
                            +${remainingCount} mais ${remainingCount === 1 ? 'membro' : 'membros'}
                        </div>
                    `;
                }

                popupContent += '</div>';

                marker.bindPopup(popupContent, {
                    maxWidth: 300,
                    className: 'custom-popup'
                });

                // Add hover effect
                marker.on('mouseover', function() {
                    this.openPopup();
                });
            });

            // Fit map to show all markers
            const bounds = L.latLngBounds(cityGroups.map(cg => [cg.Latitude, cg.Longitude]));
            map.fitBounds(bounds, { padding: [50, 50] });
        }
        
        console.log('Member map initialized successfully');
        return true;

    } catch (error) {
        console.error('Error initializing member map:', error);
        return false;
    }
};
