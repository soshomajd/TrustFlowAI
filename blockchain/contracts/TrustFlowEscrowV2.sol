// SPDX-License-Identifier: MIT
pragma solidity 0.8.36;

import {TrustFlowEscrow} from "./TrustFlowEscrow.sol";
import {SafeERC20} from "@openzeppelin/contracts/token/ERC20/utils/SafeERC20.sol";
import {IERC20} from "@openzeppelin/contracts/token/ERC20/IERC20.sol";

// New deployments only. Existing V1 contracts remain non-upgradeable and free.
contract TrustFlowEscrowV2 is TrustFlowEscrow {
    using SafeERC20 for IERC20;

    uint256 public constant PLATFORM_FEE_BPS = 1000;
    address public immutable feeRecipient;
    uint256 public platformFeesPaid;
    uint256 public freelancerNetPaid;
    mapping(uint256 => uint256) private _milestoneFees;

    error InvalidFeeRecipient();

    event FreelancerPaid(
        uint256 indexed milestoneIndex,
        uint256 grossAmount,
        uint256 platformFee,
        uint256 netAmount,
        address indexed recipient
    );

    constructor(
        address token_, address client_, address freelancer_, address arbiter_,
        uint256[] memory milestoneAmounts_, address feeRecipient_
    ) TrustFlowEscrow(token_, client_, freelancer_, arbiter_, milestoneAmounts_) {
        if (feeRecipient_ == address(0) || feeRecipient_ == address(this)) {
            revert InvalidFeeRecipient();
        }
        feeRecipient = feeRecipient_;
    }

    function getMilestoneFee(uint256 index) external view returns (uint256) {
        if (index >= this.getMilestoneCount()) revert InvalidMilestoneIndex(index);
        return _milestoneFees[index];
    }

    // Existing releasedAmount/getMilestonePaid and release/case events retain
    // GROSS award semantics, so paid fees can never be counted as refundable.
    // Only this hook pays a fee; refunds and resignation never reach it.
    function _payFreelancer(uint256 index, uint256 grossAmount) internal override {
        uint256 fee = grossAmount / 10; // Floor once per payout, in token base units.
        uint256 net = grossAmount - fee;
        _milestoneFees[index] = fee;
        platformFeesPaid += fee;
        freelancerNetPaid += net;
        if (fee > 0) token.safeTransfer(feeRecipient, fee);
        if (net > 0) token.safeTransfer(freelancer, net);
        emit FreelancerPaid(index, grossAmount, fee, net, feeRecipient);
    }
}
