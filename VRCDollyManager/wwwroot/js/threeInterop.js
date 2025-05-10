window.ThreeInterop = {
    scene: null,
    camera: null,
    renderer: null,
    dolly: null,
    curveObject: null,
    pathCurve: null,
    animationId: null,
    gridHelper: null,
    rotationAngle: 0,
    orbitRadius: 0,
    
    dollies: null,
    curvedObjects: null,
    

    // Initialize Three.js Scene with Dynamic Scaling
    initScene: function () {
        if (typeof THREE === "undefined") {
            console.error("Three.js is not loaded!");
            return;
        }

        const canvas = document.getElementById("preview");
        if (!canvas) {
            console.error("Canvas element not found!");
            return;
        }

        // Clean up previous scene if any
        this.disposeScene();

        // Create Three.js Scene
        this.scene = new THREE.Scene();
        this.scene.background = new THREE.Color(0x1E1E1E); // Dark gray background

        this.camera = new THREE.PerspectiveCamera(75, canvas.clientWidth / canvas.clientHeight, 0.1, 3000);
        this.renderer = new THREE.WebGLRenderer({ canvas, antialias: true });

        this.renderer.setSize(canvas.clientWidth, canvas.clientHeight, false);

        // Add lighting
        const light = new THREE.PointLight(0xffffff, 1, 100);
        light.position.set(10, 10, 10);
        this.scene.add(light);

        // Default Grid (Will be resized dynamically)
        this.createGrid(100);

        // Default camera position
        this.camera.position.set(0, 10, 20);
        this.camera.lookAt(new THREE.Vector3(0, 0, 0));

        this.renderer.render(this.scene, this.camera);

        // Ensure only one resize event listener
        window.removeEventListener("resize", this.onWindowResize);
        window.addEventListener("resize", () => this.onWindowResize());
    },

    // Resize Handler
    onWindowResize: function () {
        const canvas = document.getElementById("preview");
        if (!canvas || !this.camera || !this.renderer) return;

        this.camera.aspect = canvas.clientWidth / canvas.clientHeight;
        this.camera.updateProjectionMatrix();
        this.renderer.setSize(canvas.clientWidth, canvas.clientHeight, false);

        this.renderer.render(this.scene, this.camera);
    },

    // Create or Update Grid
    createGrid: function (size) {
        if (this.gridHelper) {
            this.scene.remove(this.gridHelper);
            this.gridHelper.geometry.dispose();
            this.gridHelper.material.dispose();
        }

        const divisions = Math.max(10, size / 5);
        this.gridHelper = new THREE.GridHelper(size, divisions, 0x444444, 0x444444);
        this.gridHelper.position.y = 0;
        this.scene.add(this.gridHelper);
    },

    // Render Path from Keyframes, Auto Frame & Rotate Around It
    renderPaths: function (jsonString) {
        if (!this.scene) return;

        let keyframes;
        try {
            keyframes = JSON.parse(jsonString);
        } catch (error) {
            console.error("Failed to parse JSON:", error);
            return;
        }

        if (!Array.isArray(keyframes) || keyframes.length === 0) {
            console.error("Keyframes array is empty or not an array.");
            return;
        }

        this.clearScene(); // Remove previous objects before rendering new paths

        // Group keyframes by PathIndex
        const groupedKeyframes = keyframes.reduce((groups, kf) => {
            const pathIndex = kf.pathIndex ?? 0; // Use 0 if PathIndex is missing
            if (!groups[pathIndex]) groups[pathIndex] = [];
            groups[pathIndex].push(kf);
            return groups;
        }, {});

        const colors = [0x9B54FD, 0xFF6F61, 0x6B5B95, 0x88B04B, 0xF7CAC9, 0x92A8D1]; // Some random colors for distinction
        const paths = [];
        this.dollies = [];
        this.curveObjects = [];
        
        // Render each path group
        Object.keys(groupedKeyframes).forEach((pathIndex, index) => {
            const keyframes = groupedKeyframes[pathIndex];
            let curvePoints = keyframes.map(kf => new THREE.Vector3(kf.position.x, kf.position.y, kf.position.z));

            // Compute bounding box of the path
            const boundingBox = new THREE.Box3().setFromPoints(curvePoints);
            const pathCenter = boundingBox.getCenter(new THREE.Vector3());
            const pathSize = boundingBox.getSize(new THREE.Vector3());

            // Adjust grid size dynamically based on path size
            const gridSize = Math.max(100, Math.max(pathSize.x, pathSize.z) * 2);
            this.createGrid(gridSize);

            // Reposition path so its center aligns with (0,0,0) in grid space
            const offset = new THREE.Vector3(-pathCenter.x, 0, -pathCenter.z);
            curvePoints = curvePoints.map(p => p.clone().add(offset));

            // Create the path curve with adjusted points
            const pathCurve = new THREE.CatmullRomCurve3(curvePoints);
            paths.push(pathCurve);
            
            // Render the path
            const geometry = new THREE.BufferGeometry().setFromPoints(pathCurve.getPoints(100));
            const material = new THREE.LineBasicMaterial({ color: colors[index % colors.length] });
            const curveObject = new THREE.Line(geometry, material);
            this.scene.add(curveObject);
            this.curveObjects.push(curveObject);

            // Add dolly (moving object) for this path
            const sphereGeometry = new THREE.SphereGeometry(0.5, 32, 32);
            const sphereMaterial = new THREE.MeshBasicMaterial({ color: 0xFFFFFF });
            const dolly = new THREE.Mesh(sphereGeometry, sphereMaterial);
            this.scene.add(dolly);
            this.dollies.push({ dolly, pathCurve });

            // Auto-adjust camera distance based on path size
            const maxDim = Math.max(pathSize.x, pathSize.y, pathSize.z);
            this.orbitRadius = maxDim * 1.5;
            const minHeight = 10;
            const extraHeight = pathSize.y * 1.5;
            const cameraHeight = Math.max(minHeight, extraHeight);

            // Adjust camera FOV dynamically if needed
            const fovFactor = 2 * Math.tan((this.camera.fov * Math.PI) / 360);
            let optimalDistance = maxDim / fovFactor;

            if (optimalDistance > 1000) {
                this.camera.fov = Math.max(30, this.camera.fov - 10);
                this.camera.updateProjectionMatrix();
            }

            // Set final camera position
            this.camera.position.set(optimalDistance, cameraHeight, optimalDistance);
            this.camera.lookAt(new THREE.Vector3(0, 0, 0));
        });

        // Ensure only one animation loop
        if (this.animationId) {
            cancelAnimationFrame(this.animationId);
        }

        let t = 0;
        const animateDolly = () => {
            if (!paths.length) return;

            this.animationId = requestAnimationFrame(animateDolly);

            t += 0.002;
            if (t > 1) t = 0;

            this.dollies.forEach(({ dolly, pathCurve }) => {
                const position = pathCurve.getPointAt(t);
                if (position) {
                    dolly.position.set(position.x, position.y, position.z);
                }
            });

            this.rotationAngle += 0.002;
            this.camera.position.x = Math.sin(this.rotationAngle) * this.orbitRadius;
            this.camera.position.z = Math.cos(this.rotationAngle) * this.orbitRadius;
            this.camera.position.y = this.orbitRadius / 2;
            this.camera.lookAt(new THREE.Vector3(0, 0, 0));

            this.renderer.render(this.scene, this.camera);
        };

        animateDolly();
    },

    // Clear Path and Dolly (Keep Grid)
    clearScene: function () {
        if (this.animationId) {
            cancelAnimationFrame(this.animationId);
            this.animationId = null;
        }

        if (this.curveObjects) {
            this.curveObjects.forEach(curve => this.scene.remove(curve));
            this.curveObjects = [];
        }
        if (this.dollies) {
            this.dollies.forEach(({ dolly }) => this.scene.remove(dolly));
            this.dollies = [];
        }

        this.renderer.render(this.scene, this.camera);
    },

    // Full scene disposal to prevent memory leaks
    disposeScene: function () {
        if (!this.scene) return;

        this.clearScene();

        if (this.gridHelper) {
            this.scene.remove(this.gridHelper);
            this.gridHelper.geometry.dispose();
            this.gridHelper.material.dispose();
            this.gridHelper = null;
        }

        this.renderer.dispose();
        this.scene = null;
        this.camera = null;
        this.renderer = null;
        this.dollies = null;
        this.curveObjects = null;
    }
};
