using Energy_Project.Services.Interfaces;
using Moq;
using Energy_Project.Models;
using Energy_Project.Services;
using Energy_Project.Services.Interfaces;
using Moq;

namespace SmartHomeTests
{
    /// <summary>
    /// Unit tests verifying functionality and edge cases in DeviceService.
    /// Validates device state toggling, filtering logic, and repository invocations.
    /// </summary>
    public class DeviceServiceTests
    {
        private readonly Mock<IDeviceRepository> _deviceRepo = new();

        /// <summary>
        /// Factory helper to build a DeviceService instance with a mocked repository.
        /// </summary>
        /// <returns>A new DeviceService instance</returns>
        private DeviceService CreateService()
        {
            return new DeviceService(_deviceRepo.Object);
        }

        /// <summary>
        /// Parameterized test validating that toggling an existing device yields the requested state.
        /// </summary>
        /// <param name="deviceId">Target device identifier</param>
        /// <param name="targetState">Desired power state</param>
        /// <param name="expected">Expected boolean outcome</param>
        [Theory]
        [InlineData(10, true, true)]
        [InlineData(10, false, false)]
        [InlineData(25, true, true)]
        public void ToggleDevice_ExistingDevice_ReturnsRequestedState(int deviceId, bool targetState, bool expected)
        {
            // Arrange
            var service = CreateService();
            var fakeDevice = new Device { Id = deviceId, Name = "Desk Fan", IsOn = !targetState };

            _deviceRepo.Setup(r => r.GetById(deviceId)).Returns(fakeDevice);

            // Act
            var actual = service.ToggleDevice(deviceId, targetState);

            // Assert
            Assert.Equal(expected, actual);
            _deviceRepo.Verify(r => r.Update(fakeDevice), Times.Once);
        }

        /// <summary>
        /// Verifies that turning on an inactive device produces a state different from its initial state.
        /// </summary>
        [Fact]
        public void ToggleDevice_TurnOnTurnedOffDevice_ReturnsStateNotEqualToInitial()
        {
            // Arrange
            var service = CreateService();
            const int DEVICE_ID = 5;
            const bool INITIAL_STATE = false;
            const bool TURN_ON = true;
            var fakeDevice = new Device { Id = DEVICE_ID, Name = "Floor Lamp", IsOn = INITIAL_STATE };

            _deviceRepo.Setup(r => r.GetById(DEVICE_ID)).Returns(fakeDevice);

            // Act
            var actual = service.ToggleDevice(DEVICE_ID, TURN_ON);

            // Assert
            Assert.NotEqual(INITIAL_STATE, actual);
            Assert.True(actual);
        }

        /// <summary>
        /// Verifies that turning off an active device returns false.
        /// </summary>
        [Fact]
        public void ToggleDevice_TurnOffActiveDevice_ReturnsFalse()
        {
            // Arrange
            var service = CreateService();
            const int DEVICE_ID = 4;
            const bool TURN_OFF = false;
            var fakeDevice = new Device { Id = DEVICE_ID, Name = "Coffee Maker", IsOn = true };

            _deviceRepo.Setup(r => r.GetById(DEVICE_ID)).Returns(fakeDevice);

            // Act
            var actual = service.ToggleDevice(DEVICE_ID, TURN_OFF);

            // Assert
            Assert.False(actual);
        }

        /// <summary>
        /// Verifies that attempting to toggle a non-existent device throws an ArgumentException.
        /// </summary>
        [Fact]
        public void ToggleDevice_DeviceNotFound_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateService();
            const int INVALID_ID = 404;
            const bool TURN_ON = true;
            Device? nullDevice = null;

            _deviceRepo.Setup(r => r.GetById(INVALID_ID)).Returns(nullDevice);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => service.ToggleDevice(INVALID_ID, TURN_ON));
        }

        /// <summary>
        /// Ensures that the repository update method is never invoked when an invalid ID is provided.
        /// </summary>
        [Fact]
        public void ToggleDevice_InvalidId_NeverCallsRepositoryUpdate()
        {
            // Arrange
            var service = CreateService();
            const int INVALID_ID = 999;
            const bool TURN_ON = true;
            Device? nullDevice = null;

            _deviceRepo.Setup(r => r.GetById(INVALID_ID)).Returns(nullDevice);

            // Act
            var capturedException = Record.Exception(() => service.ToggleDevice(INVALID_ID, TURN_ON));

            // Assert
            Assert.NotNull(capturedException);
            _deviceRepo.Verify(r => r.Update(It.IsAny<Device>()), Times.Never);
        }

        /// <summary>
        /// Verifies that toggling an existing device calls repository.Update() with the exact updated model.
        /// </summary>
        [Fact]
        public void ToggleDevice_ValidDevice_CallsRepositoryUpdateWithMatchingState()
        {
            // Arrange
            var service = CreateService();
            const int DEVICE_ID = 12;
            const bool TARGET_STATE = true;
            var fakeDevice = new Device { Id = DEVICE_ID, Name = "Humidifier", IsOn = false };

            _deviceRepo.Setup(r => r.GetById(DEVICE_ID)).Returns(fakeDevice);

            // Act
            service.ToggleDevice(DEVICE_ID, TARGET_STATE);

            // Assert
            _deviceRepo.Verify(r => r.Update(It.Is<Device>(d => d.Id == DEVICE_ID && d.IsOn == TARGET_STATE)), Times.Once);
        }

        /// <summary>
        /// Verifies that active devices collection contains active entities.
        /// </summary>
        [Fact]
        public void GetActiveDevices_PopulatedCollection_ReturnsCollectionContainingActiveDevice()
        {
            // Arrange
            var service = CreateService();
            var activeDevice = new Device { Id = 1, Name = "Refrigerator", IsOn = true };
            var inactiveDevice = new Device { Id = 2, Name = "Microwave", IsOn = false };
            var fakeList = new List<Device> { activeDevice, inactiveDevice };

            _deviceRepo.Setup(r => r.GetAll()).Returns(fakeList);

            // Act
            var result = service.GetActiveDevices();

            // Assert
            Assert.Contains(activeDevice, result);
            Assert.NotEmpty(result);
        }

        /// <summary>
        /// Verifies that an empty collection is returned when all registered devices are powered down.
        /// </summary>
        [Fact]
        public void GetActiveDevices_AllDevicesTurnedOff_ReturnsEmptyCollection()
        {
            // Arrange
            var service = CreateService();
            var fakeList = new List<Device>
            {
                new() { Id = 1, Name = "Toaster", IsOn = false },
                new() { Id = 2, Name = "Blender", IsOn = false }
            };

            _deviceRepo.Setup(r => r.GetAll()).Returns(fakeList);

            // Act
            var result = service.GetActiveDevices();

            // Assert
            Assert.Empty(result);
        }

        /// <summary>
        /// Verifies that retrieving active devices invokes GetAll() on the repository at least once.
        /// </summary>
        [Fact]
        public void GetActiveDevices_Invocation_CallsRepositoryGetAllAtLeastOnce()
        {
            // Arrange
            var service = CreateService();
            _deviceRepo.Setup(r => r.GetAll()).Returns(new List<Device>());

            // Act
            service.GetActiveDevices();

            // Assert
            _deviceRepo.Verify(r => r.GetAll(), Times.AtLeastOnce);
        }

        /// <summary>
        /// Direct test ensuring the repository mock returns null when requesting an unregistered device ID.
        /// </summary>
        [Fact]
        public void Repository_GetById_DeviceDoesNotExist_ReturnsNull()
        {
            // Arrange
            const int UNKNOWN_ID = 67;
            _deviceRepo.Setup(r => r.GetById(UNKNOWN_ID)).Returns((Device?)null);

            // Act
            var result = _deviceRepo.Object.GetById(UNKNOWN_ID);

            // Assert
            Assert.Null(result);
        }
    }
}
