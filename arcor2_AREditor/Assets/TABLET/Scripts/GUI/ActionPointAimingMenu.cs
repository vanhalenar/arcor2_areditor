using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Michsky.UI.ModernUIPack;
using System.Linq;
using UnityEngine.UI;
using DanielLochner.Assets.SimpleSideMenu;
using Base;
using System;

[RequireComponent(typeof(SimpleSideMenu))]
public class ActionPointAimingMenu : MonoBehaviour, IMenu
{
    public Base.ActionPoint CurrentActionPoint;

    public GameObject UpdatePoseBlock, UpdateJointsBlock;
    [SerializeField]
    private TMPro.TMP_Text NoOrientation, NoJoints, ActionPointName;

    [SerializeField]
    private GameObject updatePositionButton, UpdatePositionToggle, UpdatePositionBlock;

    public FocusConfirmationDialog FocusConfirmationDialog;

    public DropdownParameter RobotsList, EndEffectorList, OrientationsList;
    public DropdownParameterJoints JointsList;

    [SerializeField]
    private InputDialog inputDialog;

    private SimpleSideMenu SideMenu;

    [SerializeField]
    private Button UpdateJointsBtn, UpdateOrientationBtn;

    private void Start() {
        SideMenu = GetComponent<SimpleSideMenu>();
    }

    public void UpdateMenu(string preselectedOrientation = null) {
        ActionPointName.text = CurrentActionPoint.Data.Name;
        CustomDropdown robotsListDropdown = RobotsList.Dropdown;
        robotsListDropdown.dropdownItems.Clear();

        RobotsList.gameObject.GetComponent<DropdownRobots>().Init(OnRobotChanged, false);
        if (robotsListDropdown.dropdownItems.Count == 0) {
            UpdatePositionBlock.SetActive(false);
        } else {
            OnRobotChanged((string) RobotsList.GetValue());
            UpdatePositionBlock.SetActive(true);

        }

        UpdateOrientations(preselectedOrientation);

    }

    public void UpdateOrientations(string preselectedOrientation = null) {
        CustomDropdown orientationDropdown = OrientationsList.Dropdown;
        orientationDropdown.dropdownItems.Clear();
        int selectedItem = 0;
        foreach (IO.Swagger.Model.NamedOrientation orientation in CurrentActionPoint.GetNamedOrientations()) {
            CustomDropdown.Item item = new CustomDropdown.Item {
                itemName = orientation.Name
            };
            orientationDropdown.dropdownItems.Add(item);
            if (preselectedOrientation == orientation.Id) {
                selectedItem = orientationDropdown.dropdownItems.Count - 1;
            }

        }
        if (orientationDropdown.dropdownItems.Count == 0) {
            OrientationsList.gameObject.SetActive(false);
            NoOrientation.gameObject.SetActive(true);
            UpdateOrientationBtn.interactable =false;
        } else {
            NoOrientation.gameObject.SetActive(false);
            OrientationsList.gameObject.SetActive(true);
            orientationDropdown.enabled = true;
            orientationDropdown.selectedItemIndex = selectedItem;
            orientationDropdown.SetupDropdown();
            UpdateOrientationBtn.interactable = true;
        }
        

    }


    public void UpdateJoints(string robot_id, string selectedJoints = null) {
        if (robot_id == null)
            return;
        CustomDropdown jointsDropdown = JointsList.Dropdown;

        JointsList.PutData(CurrentActionPoint.GetAllJoints(true, robot_id).Values.ToList(), selectedJoints, null, CurrentActionPoint.Data.Name);

        if (jointsDropdown.dropdownItems.Count > 0) {
            NoJoints.gameObject.SetActive(false);
            JointsList.gameObject.SetActive(true);
            UpdateJointsBtn.interactable = true;
        } else {
            JointsList.gameObject.SetActive(false);
            NoJoints.gameObject.SetActive(true);
            UpdateJointsBtn.interactable = false;
        }
    }

    private void OnRobotChanged(string robot_name) {
        EndEffectorList.Dropdown.dropdownItems.Clear();
        
        try {
            string robotId = SceneManager.Instance.RobotNameToId(robot_name);
            EndEffectorList.gameObject.GetComponent<DropdownEndEffectors>().Init(robotId);
            if (EndEffectorList.Dropdown.dropdownItems.Count == 0) {
                UpdatePoseBlock.SetActive(false);
                UpdateJointsBlock.SetActive(true);
            } else {
                UpdatePoseBlock.SetActive(true);
                UpdateJointsBlock.SetActive(false);
            }
            UpdateJoints(robot_name);
        } catch (KeyNotFoundException ex) {
            Debug.LogError(ex);
            Notifications.Instance.ShowNotification("Failed to load end effectors", "");
        }
        
    }




    public void ShowAddOrientationDialog() {
        inputDialog.Open("Create new named orientation",
                         "Please set name of the new orientation",
                         "Name",
                         CurrentActionPoint.GetFreeOrientationName(),
                         () => AddOrientation(inputDialog.GetValue()),
                         () => inputDialog.Close());
    }

    public async void AddOrientation(string name) {
        Debug.Assert(CurrentActionPoint != null);
        IO.Swagger.Model.Orientation orientation = new IO.Swagger.Model.Orientation();
        if (CurrentActionPoint.Parent != null) {
            orientation = DataHelper.QuaternionToOrientation(TransformConvertor.UnityToROS(Quaternion.Inverse(CurrentActionPoint.Parent.GetTransform().rotation)));
        }
        
        bool success = await Base.GameManager.Instance.AddActionPointOrientation(CurrentActionPoint, orientation, name);
        if (success) {
            inputDialog.Close();
        }
        UpdateOrientations();
    }

    public void ShowAddJointsDialog() {
        inputDialog.Open("Create new joints configuration",
                         "Please set name of the new joints configuration",
                         "Name",
                         CurrentActionPoint.GetFreeJointsName(),
                         () => AddJoints(inputDialog.GetValue()),
                         () => inputDialog.Close());
    }

    public async void AddJoints(string name) {
        Debug.Assert(CurrentActionPoint != null);
        bool success = await Base.GameManager.Instance.AddActionPointJoints(CurrentActionPoint, name, (string) RobotsList.GetValue());
        if (success) {
            inputDialog.Close();
        }
        UpdateJoints((string) RobotsList.GetValue());
    }


    public void FocusJoints() {
        CustomDropdown jointsDropdown = JointsList.Dropdown;
        if (jointsDropdown.dropdownItems.Count == 0) {
            Base.NotificationsModernUI.Instance.ShowNotification("Failed to update joints", "");
            return;
        }
        try {
            string robotId = SceneManager.Instance.RobotNameToId((string) RobotsList.GetValue());
            Base.GameManager.Instance.UpdateActionPointJoints(robotId, (string) JointsList.GetValue());
            Base.NotificationsModernUI.Instance.ShowNotification("Joints updated sucessfully", "");
            
            UpdateJoints(robotId, (string) JointsList.GetValue());
        } catch (Exception ex) when (ex is Base.RequestFailedException || ex is KeyNotFoundException) {
            Base.NotificationsModernUI.Instance.ShowNotification("Failed to update joints", ex.Message);
        }
        
    }

    public void ShowFocusConfirmationDialog() {
        if (RobotsList.Dropdown.dropdownItems.Count == 0 ||
            EndEffectorList.Dropdown.dropdownItems.Count == 0 ||
            OrientationsList.Dropdown.dropdownItems.Count == 0) {
            Base.Notifications.Instance.ShowNotification("Failed to update orientation.", "Something is not selected");
            return;
        }
        CustomDropdown robotsListDropdown = RobotsList.Dropdown;
        CustomDropdown endEffectorDropdown = EndEffectorList.Dropdown;
        CustomDropdown orientationDropdown = OrientationsList.Dropdown;
        if (endEffectorDropdown.dropdownItems.Count == 0) {
            FocusConfirmationDialog.EndEffectorId = "";
        } else {
            FocusConfirmationDialog.EndEffectorId = endEffectorDropdown.selectedText.text;
        }
        FocusConfirmationDialog.RobotName = robotsListDropdown.selectedText.text;

        FocusConfirmationDialog.OrientationId = CurrentActionPoint.GetNamedOrientationByName(orientationDropdown.selectedText.text).Id;
        FocusConfirmationDialog.OrientationName = orientationDropdown.selectedText.text;
        FocusConfirmationDialog.UpdatePosition = UpdatePositionToggle.GetComponent<Toggle>().isOn;
        FocusConfirmationDialog.ActionPointId = CurrentActionPoint.Data.Id;
        FocusConfirmationDialog.ActionPointName = CurrentActionPoint.Data.Name;
        if (FocusConfirmationDialog.Init())
            FocusConfirmationDialog.WindowManager.OpenWindow();
    }

    public void ShowMenu(Base.ActionPoint actionPoint, string preselectedOrientation = null) {
        CurrentActionPoint = actionPoint;
        UpdateMenu(preselectedOrientation);
        SideMenu.Open();
    }

    public void Close() {
        SideMenu.Close();
    }

    public void UpdateMenu() {
        UpdateMenu(null);
    }
}
